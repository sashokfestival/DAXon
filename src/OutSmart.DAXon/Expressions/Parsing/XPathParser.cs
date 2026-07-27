////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class XPathParser
    {
        private static readonly IntToIntHashMap operatorPrecedenceTable = new IntToIntHashMap(30);

        /* Must be in alphabetical order, since a binary search is used */
        private static readonly string[] reservedFunctionNames31 = new string[]
        {
            "array",
            "attribute",
            "comment",
            "document-node",
            "element",
            "empty-sequence",
            "function",
            "if",
            "item",
            "map",
            "namespace-node",
            "node",
            "processing-instruction",
            "schema-attribute",
            "schema-element",
            "switch",
            "text",
            "typeswitch"
        };
        private static readonly string[] reservedFunctionNames40 = new string[]
        {
            "attribute",
            "comment",
            "document-node",
            "element",
            "empty-sequence",
            "fn",
            "function",
            "if",
            "item",
            "namespace-node",
            "node",
            "processing-instruction",
            "schema-attribute",
            "schema-element",
            "switch",
            "text",
            "typeswitch"
        };
        protected Tokenizer t;
        protected IStaticContext env;
        protected IndexedStack<ILocalBinding> rangeVariables = new IndexedStack<ILocalBinding>();
        // The stack holds a list of range variables that are in scope.
        // Each entry on the stack is a IBinding object containing details
        // of the variable.
        public IndexedStack<InlineFunctionDetails> inlineFunctionStack = new IndexedStack<InlineFunctionDetails>();
        protected QNameParser qNameParser;
        protected ParserExtension parserExtension = new ParserExtension();
        protected IIntPredicateProxy charChecker;
        protected bool allowXPath30Syntax = false;
        protected bool allowXPath30XSLTExtensions = false;
        protected bool allowXPath31Syntax = false;
        protected bool allowXPath40Syntax = false;
        protected bool allowSaxonExtensions = false;
        protected bool scanOnly = false;
        // scanOnly is set to true while attributes in direct element constructors
        // are being processed. We need to parse enclosed expressions in the attribute
        // in order to find the end of the attribute value, but we don't yet know the
        // full namespace context at this stage.
        private bool allowAbsentExpression = false;
        // allowAbsentExpression is a flag that indicates that it is acceptable
        // for the expression to be empty (that @is, to consist solely of whitespace and
        // comments). The result of parsing such an expression is equivalent to the
        // result of parsing an empty sequence literal, "()"
        protected ICodeInjector codeInjector = null;
        private IAccelerator accelerator = null;

        protected ParsedLanguage language = ParsedLanguage.XPATH; // know which language we are parsing, for diagnostics
        protected int languageVersion = 20;
        protected int catchDepth = 0;

        // .NET hardening (no upstream equivalent): Java's StackOverflowError is catchable, so upstream
        // relies on the JVM to turn a pathologically deep parse into a recoverable error. .NET's
        // StackOverflowException is uncatchable and kills the process, so we bound recursive-descent
        // nesting explicitly and raise XPST0003. The counter is the deterministic ceiling; the
        // StackGuard probe in ParseExprSingle covers threads too small to reach it (round AR).
        public const int MAX_EXPRESSION_NESTING = 3000;
        private int expressionDepth = 0;

        // Companion to the recursion guard above: the operator/postfix/path loops build a left-leaning
        // tree one level deeper per ITERATION without recursing, so a 300k-term chain (1+1+..., a[.][.]...,
        // a/b/...) escapes the recursion guard and burns O(n) work only to be rejected by the
        // static-analysis depth guard later. Capping loop iterations at the same bound rejects after
        // ~3000 tokens; N iterations imply a tree >= N deep, so anything rejected here would fail that
        // guard anyway — no new rejections.
        private void CheckIterativeDepth(int chainLength)
        {
            if (chainLength > MAX_EXPRESSION_NESTING)
            {
                Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
            }
        }

        private ILocation mostRecentLocation = Loc.NONE;

        public virtual ICodeInjector CodeInjector
        {
            get => codeInjector; set
            {
                this.codeInjector = value;
            }
        }

        private int CurrentOperatorPrecedence => OperatorPrecedence(t.currentToken);

        // Routines for handling range variables
        public virtual IndexedStack<ILocalBinding> RangeVariables
        {
            get => rangeVariables; set
            {
                this.rangeVariables = value;
            }
        }
        static XPathParser()
        {
            InitializeOperatorPrecedenceTable();
        }

        /// <summary>
        /// Create an expression parser
        /// </summary>
        public XPathParser(IStaticContext env)
        {
            this.env = env;
        }

        /// <summary>
        /// Initialize the static operator precedence table
        /// </summary>
        private static void InitializeOperatorPrecedenceTable()
        {
            operatorPrecedenceTable.DefaultValue = -1;
            IntToIntHashMap m = operatorPrecedenceTable;
            m.Put(Token.QMARK_QMARK, 3);
            m.Put(Token.BANG_BANG, 3);
            m.Put(Token.OR, 4);
            m.Put(Token.OR_ELSE, 4);
            m.Put(Token.AND, 5);
            m.Put(Token.AND_ALSO, 5);
            m.Put(Token.FEQ, 6);
            m.Put(Token.FNE, 6);
            m.Put(Token.FLT, 6);
            m.Put(Token.FGT, 6);
            m.Put(Token.FLE, 6);
            m.Put(Token.FGE, 6);
            m.Put(Token.EQUALS, 6);
            m.Put(Token.NE, 6);
            m.Put(Token.LT, 6);
            m.Put(Token.LE, 6);
            m.Put(Token.GT, 6);
            m.Put(Token.GE, 6);
            m.Put(Token.IS, 6);
            m.Put(Token.PRECEDES, 6);
            m.Put(Token.FOLLOWS, 6);
            m.Put(Token.CONCAT, 7);
            m.Put(Token.TO, 9);
            m.Put(Token.PLUS, 10);
            m.Put(Token.MINUS, 10);
            m.Put(Token.MULT, 11);
            m.Put(Token.MATH_MULT, 11);
            m.Put(Token.DIV, 11);
            m.Put(Token.MATH_DIVIDE, 11);
            m.Put(Token.IDIV, 11);
            m.Put(Token.MOD, 11);
            m.Put(Token.OTHERWISE, 12);
            m.Put(Token.UNION, 13);
            m.Put(Token.INTERSECT, 14);
            m.Put(Token.EXCEPT, 14);
            m.Put(Token.INSTANCE_OF, 15);
            m.Put(Token.TREAT_AS, 16);
            m.Put(Token.CASTABLE_AS, 17);
            m.Put(Token.CAST_AS, 18);
            m.Put(Token.FAT_ARROW, 19);
            m.Put(Token.MAPPING_ARROW, 19); // remainder commented out because not used in precedence parsing (but perhaps they could be)
            //            case Token.BANG:
            //                return 20;
            //            case Token.SLASH:
            //                return 21;
            //            case Token.SLASH_SLASH:
            //                return 22;
            //            case Token.QMARK:
            //                return 23;
        }

        public virtual void SetAccelerator(IAccelerator accelerator)
        {
            this.accelerator = accelerator;
        }

        public virtual Tokenizer GetTokenizer()
        {
            return t;
        }

        public virtual IStaticContext GetStaticContext()
        {
            return env;
        }

        public virtual void SetParserExtension(ParserExtension extension)
        {
            this.parserExtension = extension;
        }

        public virtual void SetCatchDepth(int depth)
        {
            catchDepth = depth;
        }

        public virtual void NextToken()
        {
            try
            {
                t.Next();
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage());
            }
        }

        public virtual void Expect(int token)
        {
            if (t.currentToken != token)
            {
                Grumble("expected \"" + Token.tokens[token] + "\", found " + CurrentTokenDisplay());
            }
        }

        public virtual void Grumble(string message)
        {
            Grumble(message, language == ParsedLanguage.XSLT_PATTERN ? "XTSE0340" : "XPST0003");
        }

        public virtual void Grumble(string message, string errorCode)
        {
            Grumble(message, new StructuredQName("", NamespaceUri.ERR, errorCode), -1);
        }

        public virtual void Grumble(string message, string errorCode, int offset)
        {
            Grumble(message, new StructuredQName("", NamespaceUri.ERR, errorCode), offset);
        }

        protected virtual void Grumble(string message, StructuredQName errorCode, int offset)
        {
            if (errorCode == null)
            {
                errorCode = new StructuredQName("err", NamespaceUri.ERR, "XPST0003");
            }

            string nearbyText = null;
            int line = -1;
            int column = -1;
            if (t != null)
            {
                nearbyText = t.RecentText(-1);
                if (offset == -1)
                {
                    line = t.GetLineNumber();
                    column = t.GetColumnNumber();
                }
                else
                {
                    line = t.GetLineNumber(offset);
                    column = t.GetColumnNumber(offset);
                }
            }

            ILocation loc = MakeNestedLocation(env.GetContainingLocation(), line, column, nearbyText);
            XPathException err = new XPathException(message).WithLocation(loc).AsStaticError().WithErrorCode(errorCode);
            err.SetIsSyntaxError("XPST0003".Equals(errorCode.GetLocalPart()));
            err.SetHostLanguage(GetLanguage());
            throw err;
        }

        protected virtual void Grumble(string message, StructuredQName errorCode)
        {
            Grumble(message, errorCode, -1);
        }

        protected virtual void Warning(string message, string errorCode)
        {
            if (!env.GetConfiguration().GetBooleanProperty(Feature<bool>.SUPPRESS_XPATH_WARNINGS))
            {
                string s = t.RecentText(-1);
                string prefix = (message.StartsWith("...", StringComparison.Ordinal) ? "near" : "in") + ' ' + Err.Wrap(s) + ":\n    ";
                env.IssueWarning(prefix + message, errorCode, MakeLocation());
            }
        }

        protected virtual void SetLanguage(ParsedLanguage language, int version)
        {
            if (version == 0)
            {
                version = 30; // default
            }

            if (version == 305)
            {
                version = 30;
                allowXPath30XSLTExtensions = true;
            }

            if (version == 40)
            {
                GetStaticContext().GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "XPath 4.0 Syntax", -1);
            }

            switch (language)
            {
                case ParsedLanguage.XPATH:
                    if (!(version == 20 || version == 30 || version == 31 || version == 40))
                    {
                        throw new ArgumentException("Unsupported language version " + version);
                    }

                    break;
                case ParsedLanguage.XSLT_PATTERN:
                case ParsedLanguage.SEQUENCE_TYPE:
                case ParsedLanguage.EXTENDED_ITEM_TYPE:
                    if (!(version == 20 || version == 30 || version == 31 || version == 40))
                    {
                        throw new ArgumentException("Unsupported language version " + version);
                    }

                    break;
                case ParsedLanguage.XQUERY:
                    if (!(version == 10 || version == 30 || version == 31 || version == 40))
                    {
                        throw new ArgumentException("Unsupported language version " + version);
                    }

                    break;
                default:
                    throw new ArgumentException("Unknown language " + language);
            }

            this.language = language;
            this.languageVersion = version;
            this.allowXPath30Syntax = languageVersion >= 30;
            this.allowXPath31Syntax = languageVersion >= 31;
            this.allowXPath40Syntax = languageVersion >= 40;
        }

        protected virtual string GetLanguage()
        {
            switch (language)
            {
                case ParsedLanguage.XPATH:
                    return "XPath";
                case ParsedLanguage.XSLT_PATTERN:
                    return "XSLT Pattern";
                case ParsedLanguage.SEQUENCE_TYPE:
                    return "SequenceType";
                case ParsedLanguage.XQUERY:
                    return "XQuery";
                case ParsedLanguage.EXTENDED_ITEM_TYPE:
                    return "Extended ItemType";
                default:
                    return "XPath";
            }
        }

        public virtual bool IsAllowXPath31Syntax()
        {
            return allowXPath31Syntax;
        }

        public virtual void SetQNameParser(QNameParser qp)
        {
            this.qNameParser = qp;
        }

        public virtual QNameParser GetQNameParser()
        {
            return qNameParser;
        }

        protected virtual string CurrentTokenDisplay()
        {
            if (t.currentToken == Token.NAME)
            {
                return "name \"" + t.currentTokenValue + '"';
            }
            else if (t.currentToken == Token.UNKNOWN)
            {
                return "(unknown token)";
            }
            else
            {
                return '"' + Token.tokens[t.currentToken] + '"';
            }
        }

        public virtual Expression Parse(string expression, int start, int terminator, IStaticContext env)
        {
            this.env = env;
            int languageVersion = env.GetXPathVersion();
            if (languageVersion == 20 && language == ParsedLanguage.XQUERY)
            {
                languageVersion = 10;
            }

            SetLanguage(language, languageVersion);
            Expression exp = null;
            int offset;
            if (accelerator != null && env.GetUnprefixedElementMatchingPolicy() == UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE && terminator != Token.IMPLICIT_EOF && (expression.Length - start < 30 || terminator == Token.RCURLY))
            {

                // We need the tokenizer to be visible so that the caller can ask
                // about where the expression ended within the input string
                t = new Tokenizer();
                t.languageLevel = env.GetXPathVersion();
                exp = accelerator.Parse(t, env, expression, start, terminator);
            }

            if (exp == null)
            {
                qNameParser = new QNameParser(env.GetNamespaceResolver()).WithAcceptEQName(allowXPath30Syntax).WithErrorOnBadSyntax(language == ParsedLanguage.XSLT_PATTERN ? "XTSE0340" : "XPST0003").WithErrorOnUnresolvedPrefix("XPST0081");
                charChecker = env.GetConfiguration().ValidCharacterChecker;
                t = new Tokenizer();
                t.languageLevel = env.GetXPathVersion();
                allowXPath40Syntax = t.allowSaxonExtensions = env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || t.languageLevel == 40;
                offset = t.currentTokenStartOffset;
                CustomizeTokenizer(t);
                try
                {
                    t.Tokenize(expression, start, -1);
                }
                catch (XPathException err)
                {
                    Grumble(err.GetMessage());
                }

                if (t.currentToken == terminator)
                {
                    if (allowAbsentExpression)
                    {
                        Expression result = Literal.MakeEmptySequence();
                        result.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                        SetLocation(result);
                        return result;
                    }
                    else
                    {
                        Grumble("The expression is empty");
                    }
                }

                exp = ParseExpression();
                if (t.currentToken != terminator && terminator != Token.IMPLICIT_EOF)
                {
                    if (t.currentToken == Token.EOF && terminator == Token.RCURLY)
                    {
                        Grumble("Missing curly brace after expression in value template", "XTSE0350");
                    }
                    else
                    {
                        Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of expression");
                    }
                }

                SetLocation(exp, offset);
            }

            exp.SetRetainedStaticContextThoroughly(env.MakeRetainedStaticContext());

            //exp.verifyParentPointers();
            return exp;
        }

        protected virtual void CustomizeTokenizer(Tokenizer t)
        {
        }

        public virtual Values.SequenceType ParseSequenceType(string input, IStaticContext env)
        {
            this.env = env;
            SetLanguage(ParsedLanguage.SEQUENCE_TYPE, env.GetXPathVersion());
            if (qNameParser == null)
            {
                qNameParser = new QNameParser(env.GetNamespaceResolver());
                if (languageVersion >= 30)
                {
                    qNameParser = qNameParser.WithAcceptEQName(true);
                }
            }

            language = ParsedLanguage.SEQUENCE_TYPE;
            t = new Tokenizer();
            t.languageLevel = languageVersion;
            allowXPath40Syntax = t.allowSaxonExtensions = env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || t.languageLevel == 40;
            try
            {
                t.Tokenize(input, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage());
            }

            Values.SequenceType req = ParseSequenceType();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of SequenceType");
            }

            return req;
        }

        public virtual Types.ItemType ParseExtendedItemType(string input, IStaticContext env)
        {
            this.env = env;
            SetLanguage(ParsedLanguage.EXTENDED_ITEM_TYPE, env.GetXPathVersion());
            t = new Tokenizer();
            t.languageLevel = env.GetXPathVersion();
            allowSaxonExtensions = t.allowSaxonExtensions = true;
            try
            {
                t.Tokenize(input, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage());
            }

            Types.ItemType req = ParseItemType();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of ItemType");
            }

            return req;
        }

        public virtual Values.SequenceType ParseExtendedSequenceType(string input, IStaticContext env)
        {
            this.env = env;
            language = ParsedLanguage.EXTENDED_ITEM_TYPE;
            t = new Tokenizer();
            t.languageLevel = languageVersion = 40;
            allowSaxonExtensions = t.allowSaxonExtensions = true;
            allowXPath30Syntax = allowXPath31Syntax = allowXPath40Syntax = true;
            try
            {
                t.Tokenize(input, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage());
            }

            Values.SequenceType req = ParseSequenceType();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of SequenceType");
            }

            return req;
        }

        //                     EXPRESSIONS                                              //
        public virtual Expression ParseExpression()
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = ParseExprSingle();
            List<Expression> list = null;
            while (t.currentToken == Token.COMMA)
            {

                // An expression containing a comma often contains many, so we accumulate all the
                // subexpressions into a list before creating the Block expression which reduces it to an array
                if (list == null)
                {
                    list = new List<Expression>(10);
                    list.Add(exp);
                }

                NextToken();
                Expression next = ParseExprSingle();
                SetLocation(next);
                list.Add(next);
            }

            if (list != null)
            {
                exp = Block.MakeBlock(list);
                SetLocation(exp, offset);
            }

            return exp;
        }

        public virtual Expression ParseExprSingle()
        {
            // Every nested sub-expression (function arguments, predicates, parenthesized/array/map
            // constructors) funnels through here, so one depth guard covers both XPath and XQuery.
            if (++expressionDepth > MAX_EXPRESSION_NESTING)
            {
                expressionDepth--;
                Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
            }

            // The counter above is the Java-parity ceiling; on a thread whose stack cannot hold even
            // MAX_EXPRESSION_NESTING recursion levels (a default 1 MB thread dies near ~2300), the
            // stack-adaptive probe raises the same XPST0003 before the uncatchable SOE.
            try
            {
                StackGuard.Probe();
            }
            catch (RecursionDepthError)
            {
                expressionDepth--;
                Grumble("Expression is too deeply nested (insufficient stack on this thread)", "XPST0003");
            }

            try
            {
                return ParseExprSingleImpl();
            }
            finally
            {
                expressionDepth--;
            }
        }

        private Expression ParseExprSingleImpl()
        {
            Expression e = parserExtension.ParseExtendedExprSingle(this);
            if (e != null)
            {
                return e;
            }


            // Short-circuit for a single-token expression
            int peek = t.PeekAhead();
            if (peek == Token.EOF || peek == Token.COMMA || peek == Token.RPAR || peek == Token.RSQB)
            {
                switch (t.currentToken)
                {
                    case Token.STRING_LITERAL:
                        return ParseStringLiteral(true);
                    case Token.NUMBER:
                        return ParseNumericLiteral(true);
                    case Token.HEX_INTEGER:
                        return ParseHexLiteral(true);
                    case Token.BINARY_INTEGER:
                        return ParseBinaryLiteral(true);
                    case Token.NAME:
                    case Token.PREFIX:
                    case Token.SUFFIX:
                    case Token.STAR:
                        return ParseBasicStep(true);
                    case Token.DOT:
                        NextToken();
                        Expression cie = new ContextItemExpression();
                        SetLocation(cie);
                        return cie;
                    case Token.DOTDOT:
                        NextToken();
                        Expression pne = new AxisExpression(AxisInfo.PARENT, null);
                        SetLocation(pne);
                        return pne;
                    case Token.EOF:
                    default:
                        break;
                }
            }

            switch (t.currentToken)
            {
                case Token.EOF:
                    Grumble("Expected an expression, but reached the end of the input");
                    return null;
                case Token.FOR:
                case Token.LET:
                case Token.FOR_MEMBER:
                case Token.FOR_SLIDING:
                case Token.FOR_TUMBLING:
                    return ParseFLWORExpression();
                case Token.SOME:
                case Token.EVERY:
                    return ParseQuantifiedExpression();
                case Token.IF:
                    return ParseIfExpression();
                case Token.SWITCH:
                    return ParseSwitchExpression();
                case Token.SWITCH_CASE:
                    return ParseSwitchExpression();
                case Token.TYPESWITCH:
                    return ParseTypeswitchExpression();
                case Token.KEYWORD_CURLY:
                    if (t.currentTokenValue.Equals("try"))
                    {
                        return ParseTryCatchExpression();
                    }


                    // else drop through
                    goto default;
                default:
                    Expression e1 = ParseBinaryExpression(ParseUnaryExpression(), 4);

                    // Process ternary conditional
                    if (t.currentToken == Token.QMARK_QMARK)
                    {
                        if (!allowXPath40Syntax)
                        {
                            Grumble("Ternary conditionals (A ?? B !! C) require XPath 4.0 to be enabled (also, note this syntax will be withdrawn)");
                        }

                        return ParseTernaryExpression(e1);
                    }

                    return e1;
            }
        }

        /// <summary>
        /// Parse a ternary conditional expression
        /// </summary>
        private Expression ParseTernaryExpression(Expression condition)
        {
            NextToken();
            Expression e2 = ParseExprSingle();
            Expect(Token.BANG_BANG);
            NextToken();
            Expression e3 = ParseExprSingle();
            return Choose.MakeConditional(condition, e2, e3);
        }

        public virtual Expression ParseBinaryExpression(Expression lhs, int minPrecedence)
        {
            int chain = 0;
            while (CurrentOperatorPrecedence >= minPrecedence)
            {
                CheckIterativeDepth(++chain);
                int offset = t.currentTokenStartOffset;
                int @operator = t.currentToken;
                int prec = CurrentOperatorPrecedence;
                switch (@operator)
                {
                    case Token.INSTANCE_OF:
                    case Token.TREAT_AS:
                        NextToken();
                        Values.SequenceType seq = ParseSequenceType();
                        lhs = MakeSequenceTypeExpression(lhs, @operator, seq);
                        SetLocation(lhs, offset);
                        if (CurrentOperatorPrecedence >= prec)
                        {
                            Grumble("Left operand of '" + Token.tokens[t.currentToken] + "' needs parentheses");
                        }

                        break;
                    case Token.CAST_AS:
                    case Token.CASTABLE_AS:
                        NextToken();
                        ICastingTarget at;
                        if (allowXPath40Syntax && t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("union"))
                        {

                            // Saxon 9.8 / XPath 4.0 proposed extension
                            at = (ICastingTarget)ParseItemType();
                        }
                        else
                        {
                            Expect(Token.NAME);
                            if (scanOnly)
                            {
                                at = BuiltInAtomicType.STRING;
                            }
                            else
                            {
                                StructuredQName sq = null;
                                try
                                {
                                    sq = qNameParser.Parse(t.currentTokenValue, env.GetDefaultElementNamespace());
                                }
                                catch (XPathException e)
                                {
                                    Grumble(e.GetMessage(), e.ErrorCodeQName);
                                }

                                Types.ItemType alias = env.ResolveTypeAlias(sq);
                                if (alias != null)
                                {
                                    if (alias is ICastingTarget)
                                    {
                                        at = (ICastingTarget)alias;
                                    }
                                    else
                                    {
                                        Grumble("The type " + t.currentTokenValue + " cannot be used as the target of a cast");
                                        at = null;
                                    }
                                }
                                else
                                {
                                    at = GetSimpleType(t.currentTokenValue);
                                }
                            }

                            NextToken();
                        }

                        if (at == BuiltInAtomicType.ANY_ATOMIC)
                        {
                            Grumble("No value is castable to xs:anyAtomicType", "XPST0080");
                        }

                        if (at == BuiltInAtomicType.NOTATION)
                        {
                            Grumble("No value is castable to xs:NOTATION", "XPST0080");
                        }

                        bool allowEmpty = t.currentToken == Token.QMARK;
                        if (allowEmpty)
                        {
                            NextToken();
                        }

                        lhs = MakeSingleTypeExpression(lhs, @operator, at, allowEmpty);
                        SetLocation(lhs, offset);
                        if (CurrentOperatorPrecedence >= prec)
                        {
                            Grumble("Left operand of '" + Token.tokens[t.currentToken] + "' needs parentheses");
                        }

                        break;
                    case Token.FAT_ARROW:
                        lhs = ParseArrowPostfix(lhs);
                        break;
                    case Token.MAPPING_ARROW:
                        CheckLanguageVersion40();
                        lhs = ParseMappingArrowPostfix(lhs);
                        break;
                    default:
                        NextToken();
                        Expression rhs = ParseUnaryExpression();
                        while (CurrentOperatorPrecedence > prec)
                        {
                            rhs = ParseBinaryExpression(rhs, CurrentOperatorPrecedence);
                        }

                        if (CurrentOperatorPrecedence == prec && !AllowMultipleOperators())
                        {
                            string tok = Token.tokens[t.currentToken];
                            string message = "Left operand of '" + Token.tokens[t.currentToken] + "' needs parentheses";
                            if (tok.Equals("<") || tok.Equals(">"))
                            {

                                // Example input: return <a>3</a><b>4</b> - bug 2659
                                message += ". Or perhaps an XQuery element constructor appears where it is not allowed";
                            }

                            Grumble(message);
                        }

                        lhs = MakeBinaryExpression(lhs, @operator, rhs);
                        SetLocation(lhs, offset);
                        break;
                }
            }

            return lhs;
        }

        private bool AllowMultipleOperators()
        {
            switch (t.currentToken)
            {
                case Token.FEQ:
                case Token.FNE:
                case Token.FLE:
                case Token.FLT:
                case Token.FGE:
                case Token.FGT:
                case Token.EQUALS:
                case Token.NE:
                case Token.LE:
                case Token.LT:
                case Token.GE:
                case Token.GT:
                case Token.IS:
                case Token.PRECEDES:
                case Token.FOLLOWS:
                case Token.TO:
                    return false;
                default:
                    return true;
            }
        }

        public static int OperatorPrecedence(int @operator)
        {
            return operatorPrecedenceTable[@operator];
        }

        private Expression MakeBinaryExpression(Expression lhs, int @operator, Expression rhs)
        {
            switch (@operator)
            {
                case Token.OR:
                    return new OrExpression(lhs, rhs);
                case Token.AND:
                    return new AndExpression(lhs, rhs);
                case Token.FEQ:
                case Token.FNE:
                case Token.FLE:
                case Token.FLT:
                case Token.FGE:
                case Token.FGT:
                    return new ValueComparison(lhs, @operator, rhs);
                case Token.EQUALS:
                case Token.NE:
                case Token.LE:
                case Token.LT:
                case Token.GE:
                case Token.GT:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeGeneralComparison(lhs, @operator, rhs);
                case Token.IS:
                case Token.PRECEDES:
                case Token.FOLLOWS:
                    return new IdentityComparison(lhs, @operator, rhs);
                case Token.TO:
                    return new RangeExpression(lhs, rhs);
                case Token.CONCAT:
                    {
                        if (!allowXPath30Syntax)
                        {
                            Grumble("Concatenation operator ('||') requires XPath 3.0 to be enabled");
                        }

                        RetainedStaticContext rsc = new RetainedStaticContext(env);
                        Configuration config = env.GetConfiguration();
                        BuiltInFunctionSet lib = config.GetXPathFunctionSet(env.GetXPathVersion());
                        if (lhs.IsCallOn(typeof(Concat)))
                        {
                            Expression[] args = ((SystemFunctionCall)lhs).Arguments;
                            Expression[] newArgs = new Expression[args.Length + 1];
                            Array.Copy(args, 0, newArgs, 0, args.Length);
                            newArgs[args.Length] = rhs;
                            SystemFunction concat = lib.MakeFunction("concat", newArgs.Length);
                            concat.SetRetainedStaticContext(rsc);
                            return concat.MakeFunctionCall(newArgs);
                        }
                        else
                        {
                            SystemFunction concat = lib.MakeFunction("concat", 2);
                            concat.SetRetainedStaticContext(rsc);
                            Expression[] args = new Expression[]
                            {
                            lhs,
                            rhs
                            };
                            return concat.MakeFunctionCall(args);
                        }
                    }

                case Token.PLUS:
                case Token.MINUS:
                case Token.MULT:
                case Token.DIV:
                case Token.IDIV:
                case Token.MOD:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(lhs, @operator, rhs);
                case Token.MATH_MULT:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(lhs, Token.MULT, rhs);
                case Token.MATH_DIVIDE:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(lhs, Token.DIV, rhs);
                case Token.OTHERWISE:
                    return MakeOtherwiseExpression(lhs, rhs);
                case Token.UNION:
                case Token.INTERSECT:
                case Token.EXCEPT:
                    return new VennExpression(lhs, @operator, rhs);
                case Token.OR_ELSE:
                    {

                        // Compile ($x orElse $y) as (if ($x) then true() else boolean($y))
                        RetainedStaticContext rsc = new RetainedStaticContext(env);
                        rhs = SystemFunction.MakeCall("boolean", rsc, rhs);
                        return Choose.MakeConditional(lhs, Literal.MakeLiteral(BooleanValue.TRUE), rhs);
                    }

                case Token.AND_ALSO:
                    {

                        // Compile ($x andAlso $y) as (if ($x) then boolean($y) else false())
                        RetainedStaticContext rsc = new RetainedStaticContext(env);
                        rhs = SystemFunction.MakeCall("boolean", rsc, rhs);
                        return Choose.MakeConditional(lhs, rhs, Literal.MakeLiteral(BooleanValue.FALSE));
                    }

                default:
                    throw new ArgumentException(Token.tokens[@operator]);
            }
        }

        private Expression MakeOtherwiseExpression(Expression lhs, Expression rhs)
        {
            CheckLanguageVersion40();
            LetExpression let = new LetExpression();
            let.SetVariableQName(new StructuredQName("vv", NamespaceUri.ANONYMOUS, "n" + lhs.GetHashCode()));
            let.Sequence = lhs;
            let.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
            LocalVariableReference v1 = new LocalVariableReference(let.GetVariableQName());
            v1.SetBinding(let);
            let.AddReference(v1, false);
            LocalVariableReference v2 = new LocalVariableReference(let.GetVariableQName());
            v2.SetBinding(let);
            let.AddReference(v2, false);
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            Expression[] conditions = new Expression[]
            {
                SystemFunction.MakeCall("exists", rsc, v1),
                Literal.MakeLiteral(BooleanValue.TRUE, lhs)
            };
            Expression[] actions = new Expression[]
            {
                v2,
                rhs
            };
            let.SetAction(new Choose(conditions, actions));
            return let;
        }

        private Expression MakeSequenceTypeExpression(Expression lhs, int @operator, Values.SequenceType type)
        {
            switch (@operator)
            {
                case Token.INSTANCE_OF:
                    return new InstanceOfExpression(lhs, type);
                case Token.TREAT_AS:
                    return TreatExpression.Make(lhs, type);
                default:
                    throw new ArgumentException();
            }
        }

        private Expression MakeSingleTypeExpression(Expression lhs, int @operator, ICastingTarget type, bool allowEmpty)
        {
            if (type is IAtomicType && !(type == ErrorType.GetInstance()))
            {
                switch (@operator)
                {
                    case Token.CASTABLE_AS:
                        CastableExpression castable = new CastableExpression(lhs, (IAtomicType)type, allowEmpty);
                        if (lhs is StringLiteral)
                        {
                            castable.SetOperandIsStringLiteral(true);
                        }

                        return castable;
                    case Token.CAST_AS:
                        CastExpression cast = new CastExpression(lhs, (IAtomicType)type, allowEmpty);
                        if (lhs is StringLiteral)
                        {
                            cast.SetOperandIsStringLiteral(true);
                        }

                        return cast;
                    default:
                        throw new ArgumentException();
                }
            }
            else if (allowXPath30Syntax)
            {
                switch (@operator)
                {
                    case Token.CASTABLE_AS:
                        if (type is IUnionType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            UnionCastableFunction ucf = new UnionCastableFunction((IUnionType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(ucf, new Expression[] { lhs });
                        }
                        else if (type is IListType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            ListCastableFunction lcf = new ListCastableFunction((IListType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(lcf, new Expression[] { lhs });
                        }

                        break;
                    case Token.CAST_AS:
                        if (type is IUnionType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            UnionConstructorFunction ucf = new UnionConstructorFunction((IUnionType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(ucf, new Expression[] { lhs });
                        }
                        else if (type is IListType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            ListConstructorFunction lcf = new ListConstructorFunction((IListType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(lcf, new Expression[] { lhs });
                        }

                        break;
                    default:
                        throw new ArgumentException();
                }


                //                throw new XPathException("Cannot cast to xs:anySimpleType", "XPST0080");
                //            } else {
                throw new XPathException("Cannot cast to " + type.GetType(), "XPST0051"); //            }
            }
            else
            {
                throw new XPathException("Casting to list or union types requires XPath 3.0 to be enabled", "XPST0051");
            }
        }

        protected virtual Expression ParseTypeswitchExpression()
        {
            Grumble("typeswitch is not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseSwitchExpression()
        {
            Grumble("switch is not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseValidateExpression()
        {
            Grumble("validate{} expressions are not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseExtensionExpression()
        {
            Grumble("extension expressions (#...#) are not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseTryCatchExpression()
        {
            Grumble("try/catch expressions are not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseFLWORExpression()
        {
            if (t.currentToken == Token.LET && !allowXPath30Syntax)
            {
                Grumble("'let' is not permitted in XPath 2.0");
            }

            if (t.currentToken == Token.FOR_SLIDING || t.currentToken == Token.FOR_TUMBLING)
            {
                Grumble("sliding/tumbling windows can only be used in XQuery");
            }

            if (t.currentToken == Token.FOR_MEMBER && !allowXPath40Syntax)
            {
                Grumble("'for member' requires XPath 4.0 to be enabled");
            }

            if (t.currentToken == Token.LET)
            {
                return ParseLetExpression();
            }
            else
            {
                return ParseForExpression();
            }
        }

        private Expression ParseForExpression()
        {
            int clauses = 0;
            int offset;
            int @operator = t.currentToken;
            Assignation first = null;
            Assignation previous = null;
            do
            {
                bool forMember = false;
                offset = t.currentTokenStartOffset;
                NextToken();
                if (IsKeyword("member") && clauses > 0)
                {
                    forMember = true;
                    NextToken();
                }

                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;

                // declare the range variable
                Assignation firstClause;
                Assignation lastClause;
                if (@operator == Token.FOR)
                {
                    firstClause = lastClause = new ForExpression();
                    firstClause.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
                }
                else if (@operator == Token.FOR_MEMBER)
                {

                    // "for member $m in $array" compiles to "for $temp in array:members($array) let $m := $temp?value"
                    firstClause = new ForExpression();
                    firstClause.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
                    firstClause.SetVariableQName(new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "fm" + firstClause.GetHashCode()));
                    DeclareRangeVariable(firstClause);
                    lastClause = new LetExpression();
                    lastClause.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                    LocalVariableReference tempRef = new LocalVariableReference(firstClause);
                    LookupExpression lookup = new LookupExpression(tempRef, new StringLiteral("value"));
                    lastClause.Sequence = lookup;
                    firstClause.SetAction(lastClause);
                    forMember = true;
                    clauses++;
                } /*if (@operator == Token.LET)*/
                else
                {
                    firstClause = lastClause = new LetExpression();
                    firstClause.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                }

                clauses++;
                SetLocation(firstClause, offset);
                SetLocation(lastClause, offset);
                lastClause.SetVariableQName(MakeStructuredQName(var, NamespaceUri.NULL));
                NextToken();

                // process the "in" or ":=" clause
                Expect(@operator == Token.LET ? Token.ASSIGN : Token.IN);
                NextToken();
                Expression collection = ParseExprSingle();
                if (forMember)
                {
                    collection = ArrayFunctionSet.GetInstance(40).MakeFunction("members", 1).MakeFunctionCall(collection);
                }

                firstClause.Sequence = collection;
                DeclareRangeVariable(lastClause);
                if (previous == null)
                {
                    first = firstClause;
                }
                else
                {
                    previous.SetAction(firstClause);
                }

                previous = lastClause;
            }
            while (t.currentToken == Token.COMMA || (allowXPath40Syntax && t.currentToken == @operator));

            // process the "return" expression (called the "action")
            Expect(Token.RETURN);
            NextToken();
            previous.SetAction(ParseExprSingle());

            // undeclare all the range variables
            for (int i = 0; i < clauses; i++)
            {
                UndeclareRangeVariable();
            }

            return MakeTracer(first, first.GetVariableQName());
        }

        private Expression ParseLetExpression()
        {
            int clauses = 0;
            int offset;
            Assignation first = null;
            Assignation previous = null;
            do
            {
                offset = t.currentTokenStartOffset;
                NextToken();
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;

                // declare the range variable
                Assignation firstClause;
                Assignation lastClause;
                firstClause = lastClause = new LetExpression();
                firstClause.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                clauses++;
                SetLocation(firstClause, offset);
                SetLocation(lastClause, offset);
                lastClause.SetVariableQName(MakeStructuredQName(var, NamespaceUri.NULL));
                NextToken();

                // process the  ":=" clause
                Expect(Token.ASSIGN);
                NextToken();
                Expression collection = ParseExprSingle();
                firstClause.Sequence = collection;
                DeclareRangeVariable(lastClause);
                if (previous == null)
                {
                    first = firstClause;
                }
                else
                {
                    previous.SetAction(firstClause);
                }

                previous = lastClause;
            }
            while (t.currentToken == Token.COMMA || (allowXPath40Syntax && t.currentToken == Token.LET));

            // process the "return" expression (called the "action")
            Expect(Token.RETURN);
            NextToken();
            previous.SetAction(ParseExprSingle());

            // undeclare all the range variables
            for (int i = 0; i < clauses; i++)
            {
                UndeclareRangeVariable();
            }

            return MakeTracer(first, first.GetVariableQName());
        }

        private Expression ParseQuantifiedExpression()
        {
            int clauses = 0;
            int @operator = t.currentToken;
            QuantifiedExpression first = null;
            QuantifiedExpression previous = null;
            do
            {
                int offset = t.currentTokenStartOffset;
                NextToken();
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;
                clauses++;

                // declare the range variable
                QuantifiedExpression v = new QuantifiedExpression();
                v.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
                v.Operator = @operator;
                SetLocation(v, offset);
                v.SetVariableQName(MakeStructuredQName(var, NamespaceUri.NULL));
                NextToken();
                if (t.currentToken == Token.AS && language == ParsedLanguage.XQUERY)
                {

                    // We use this path for quantified expressions in XQuery, which permit an "as" clause
                    NextToken();
                    Values.SequenceType type = ParseSequenceType();
                    if (type.GetCardinality() != StaticProperty.EXACTLY_ONE)
                    {
                        Warning("Occurrence indicator on singleton range variable has no effect", DAXonErrorCode.SXWN9039);
                        type = Values.SequenceType.MakeSequenceType(type.PrimaryType, StaticProperty.EXACTLY_ONE);
                    }

                    v.SetRequiredType(type);
                }


                // process the "in" clause
                Expect(Token.IN);
                NextToken();
                v.Sequence = ParseExprSingle();
                DeclareRangeVariable(v);
                if (previous != null)
                {
                    previous.SetAction(v);
                }
                else
                {
                    first = v;
                }

                previous = v;
            }
            while (t.currentToken == Token.COMMA);

            // process the "return/satisfies" expression (called the "action")
            Expect(Token.SATISFIES);
            NextToken();
            previous.SetAction(ParseExprSingle());

            // undeclare all the range variables
            for (int i = 0; i < clauses; i++)
            {
                UndeclareRangeVariable();
            }

            return MakeTracer(first, first.GetVariableQName());
        }

        private Expression ParseIfExpression()
        {

            // left paren already read
            int ifoffset = t.currentTokenStartOffset;
            NextToken();
            Expression condition = ParseExpression();
            Expect(Token.RPAR);
            NextToken();
            int thenoffset = t.currentTokenStartOffset;
            if (t.currentToken == Token.LCURLY)
            {
                CheckLanguageVersion40();
                return ParseBracedActions(condition);
            }

            Expect(Token.THEN);
            NextToken();
            Expression thenExp = MakeTracer(ParseExprSingle(), null);
            SetLocation(thenExp, thenoffset);
            int elseoffset = t.currentTokenStartOffset;
            Expect(Token.ELSE);
            NextToken();
            Expression elseExp = MakeTracer(ParseExprSingle(), null);
            SetLocation(elseExp, elseoffset);
            Expression ifExp = Choose.MakeConditional(condition, thenExp, elseExp);
            SetLocation(ifExp, ifoffset);
            return MakeTracer(ifExp, null);
        }

        private Expression ParseBracedActions(Expression condition)
        {
            IList<Expression> conditions = new List<Expression>();
            IList<Expression> actions = new List<Expression>();
            conditions.Add(condition);
            NextToken();
            Expression thenExp = ParseExpression();
            actions.Add(thenExp);
            Expect(Token.RCURLY);
            t.LookAhead();
            NextToken();
            while (t.currentToken == Token.ELSE)
            {
                NextToken();
                if (t.currentToken == Token.IF)
                {

                    //                nextToken();
                    //                expect(Token.LPAR);
                    NextToken();
                    condition = ParseExpression();
                    Expect(Token.RPAR);
                    NextToken();
                    Expect(Token.LCURLY);
                    NextToken();
                    thenExp = ParseExpression();
                    Expect(Token.RCURLY);
                    t.LookAhead();
                    NextToken();
                    conditions.Add(condition);
                    actions.Add(thenExp);
                }
                else
                {
                    Expect(Token.LCURLY);
                    NextToken();
                    Expression elseExp = ParseExpression();
                    Expect(Token.RCURLY);
                    t.LookAhead();
                    NextToken();
                    conditions.Add(Literal.MakeLiteral(BooleanValue.TRUE));
                    actions.Add(elseExp);
                    break;
                }
            }

            Choose result = new Choose(conditions.ToArray(new Expression[] { }), actions.ToArray(new Expression[] { }));
            SetLocation(result);
            return result;
        }

        private Types.ItemType GetPlainType(string qname)
        {
            if (scanOnly)
            {
                return BuiltInAtomicType.STRING;
            }

            StructuredQName sq;
            try
            {
                sq = qNameParser.Parse(qname, env.GetDefaultElementNamespace());
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), e.ErrorCodeQName);
                return null;
            }

            return GetPlainType(sq);
        }

        public virtual Types.ItemType GetPlainType(StructuredQName sq)
        {
            Configuration config = env.GetConfiguration();
            NamespaceUri uri = sq.GetNamespaceUri();
            if (uri.IsEmpty())
            {
                uri = env.GetDefaultElementNamespace();
            }

            string local = sq.GetLocalPart();
            string qname = sq.DisplayName;
            bool builtInNamespace = uri.Equals(NamespaceUri.SCHEMA);
            if (builtInNamespace)
            {
                Types.ItemType t = Types.Type.GetBuiltInItemType(uri, local);
                if (t == null && "numeric".Equals(local))
                {
                    // xs:numeric is the built-in union double|float|decimal. NumericType registers itself in
                    // BuiltInType only when GetInstance() is first called (a deliberate dodge of a static-init
                    // cycle through xs:double/float/decimal), and that trigger is otherwise reached only via a
                    // function that declares an xs:numeric argument. An `instance of xs:numeric` (or a bare
                    // SequenceType) can be the first reference, so force the lazy registration here and retry.
                    NumericType.GetInstance();
                    t = Types.Type.GetBuiltInItemType(uri, local);
                }

                if (t == null)
                {
                    Grumble("Unknown atomic type " + qname, "XPST0051");
                }

                if (t is BuiltInAtomicType)
                {
                    CheckAllowedType(env, (BuiltInAtomicType)t);
                    return t;
                }
                else if (t.IsPlainType())
                {
                    return t;
                }
                else
                {
                    Grumble("The type " + qname + " is not atomic", "XPST0051");
                }
            }
            else if (uri.Equals(NamespaceUri.JAVA_TYPE))
            {
                System.Type theClass;
                try
                {
                    string className = JavaExternalObjectType.LocalNameToClassName(local);
                    theClass = config.GetType(className, false);
                }
                catch (XPathException err)
                {
                    Grumble("Unknown Java class " + local, "XPST0051");
                    return AnyItemType.GetInstance();
                }


                lock (config)
                {
                    return JavaExternalObjectType.Of(theClass);
                }
            }
            else if (uri.Equals(NamespaceUri.DOT_NET_TYPE))
            {
                return Core.Version.platform.GetExternalObjectType(config, uri, local);
            }
            else
            {
                if (allowXPath40Syntax)
                {
                    Types.ItemType it = env.ResolveTypeAlias(sq);
                    if (it != null)
                    {
                        return it;
                    }
                }

                ISchemaType st = config.GetSchemaType(sq);
                if (st == null)
                {
                    Grumble("Unknown simple type " + qname, "XPST0051");
                }
                else if (st.IsAtomicType())
                {
                    if (!env.IsImportedSchema(uri))
                    {
                        Grumble("Atomic type " + qname + " exists, but its schema definition has not been imported", "XPST0051");
                    }

                    return (IAtomicType)st;
                }
                else if (st is Types.ItemType && ((Types.ItemType)st).IsPlainType() && allowXPath30Syntax)
                {
                    if (!env.IsImportedSchema(uri))
                    {
                        Grumble("Type " + qname + " exists, but its schema definition has not been imported", "XPST0051");
                    }

                    return (Types.ItemType)st;
                }
                else if (st.IsComplexType())
                {
                    Grumble("Type (" + qname + ") is a complex type", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else if (((ISimpleType)st).IsListType())
                {
                    Grumble("Type (" + qname + ") is a list type", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else if (allowXPath30Syntax)
                {
                    Grumble("Type (" + qname + ") is a union type that cannot be used as an item type", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
                else
                {
                    Grumble("The union type (" + qname + ") cannot be used as an item type unless XPath 3.0 is enabled", "XPST0051");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
            }

            Grumble("Unknown atomic type " + qname, "XPST0051");
            return BuiltInAtomicType.ANY_ATOMIC;
        }

        private void CheckAllowedType(IStaticContext env, BuiltInAtomicType type)
        {
            string s = WhyDisallowedType(env.GetPackageData(), type);
            if (s != null)
            {
                Grumble(s, "XPST0080");
            }
        }

        public static string WhyDisallowedType(PackageData pack, BuiltInAtomicType type)
        {
            if (!type.IsAllowedInXSD10() && pack.GetConfiguration().XsdVersion == Configuration.XSD10)
            {
                return "The built-in atomic type " + type.DisplayName + " is not recognized unless XSD 1.1 is enabled";
            }

            return null;
        }

        private ICastingTarget GetSimpleType(string qname)
        {
            if (scanOnly)
            {
                return BuiltInAtomicType.STRING;
            }

            StructuredQName sq = null;
            try
            {
                sq = qNameParser.Parse(qname, env.GetDefaultElementNamespace());
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), e.ErrorCodeQName);
            }

            NamespaceUri uri = sq.GetNamespaceUri();
            string local = sq.GetLocalPart();
            bool builtInNamespace = uri.Equals(NamespaceUri.SCHEMA);
            if (builtInNamespace)
            {
                ISimpleType target = (ISimpleType)Types.Type.GetBuiltInSimpleType(uri, local);
                if (target == null)
                {
                    Grumble("Unknown simple type " + qname, allowXPath30Syntax ? "XQST0052" : "XPST0051");
                }
                else if (!(target is ICastingTarget))
                {
                    Grumble("Unsuitable type for cast: " + target.Description, "XPST0080");
                }

                ICastingTarget t = (ICastingTarget)target;
                if (t is BuiltInAtomicType)
                {
                    CheckAllowedType(env, (BuiltInAtomicType)t);
                }

                return t;
            }
            else if (uri.Equals(NamespaceUri.DOT_NET_TYPE))
            {
                return (IAtomicType)Core.Version.platform.GetExternalObjectType(env.GetConfiguration(), uri, local);
            }
            else
            {
                ISchemaType st = env.GetConfiguration().GetSchemaType(new StructuredQName("", uri, local));
                if (st == null)
                {
                    if (allowXPath30Syntax)
                    {
                        Grumble("Unknown simple type " + qname, "XQST0052");
                    }
                    else
                    {
                        Grumble("Unknown simple type " + qname, "XPST0051");
                    }

                    return BuiltInAtomicType.ANY_ATOMIC;
                }

                if (allowXPath30Syntax)
                {

                    // XPath 3.0
                    if (!env.IsImportedSchema(uri))
                    {
                        Grumble("Simple type " + qname + " exists, but its target namespace has not been imported in the static context");
                    }

                    return (ICastingTarget)st;
                }
                else
                {

                    // XPath 2.0
                    if (st.IsAtomicType())
                    {
                        if (!env.IsImportedSchema(uri))
                        {
                            Grumble("Atomic type " + qname + " exists, but its target namespace has not been imported in the static context");
                        }

                        return (IAtomicType)st;
                    }
                    else if (st.IsComplexType())
                    {
                        Grumble("Cannot cast to a complex type (" + qname + ")", "XPST0051");
                        return BuiltInAtomicType.ANY_ATOMIC;
                    }
                    else if (((ISimpleType)st).IsListType())
                    {
                        Grumble("Casting to a list type (" + qname + ") requires XPath 3.0", "XPST0051");
                        return BuiltInAtomicType.ANY_ATOMIC;
                    }
                    else
                    {
                        Grumble("casting to a union type (" + qname + ") requires XPath 3.0", "XPST0051");
                        return BuiltInAtomicType.ANY_ATOMIC;
                    }
                }
            }
        }

        public virtual Values.SequenceType ParseSequenceType()
        {
            bool disallowIndicator = t.currentTokenValue.Equals("empty-sequence");
            Types.ItemType primaryType = ParseItemType();
            if (disallowIndicator)
            {

                // No occurrence indicator allowed
                return Values.SequenceType.MakeSequenceType(primaryType, StaticProperty.EMPTY);
            }

            int occurrenceFlag = ParseOccurrenceIndicator();
            return Values.SequenceType.MakeSequenceType(primaryType, occurrenceFlag);
        }

        public virtual int ParseOccurrenceIndicator()
        {
            int occurrenceFlag;
            switch (t.currentToken)
            {
                case Token.STAR:
                case Token.MULT:

                    // "*" will be tokenized different ways depending on what precedes it
                    occurrenceFlag = StaticProperty.ALLOWS_ZERO_OR_MORE;

                    // Make the tokenizer ignore the occurrence indicator when classifying the next token
                    t.currentToken = Token.RPAR;
                    NextToken();
                    break;
                case Token.PLUS:
                    occurrenceFlag = StaticProperty.ALLOWS_ONE_OR_MORE;

                    // Make the tokenizer ignore the occurrence indicator when classifying the next token
                    t.currentToken = Token.RPAR;
                    NextToken();
                    break;
                case Token.QMARK:
                    occurrenceFlag = StaticProperty.ALLOWS_ZERO_OR_ONE;

                    // Make the tokenizer ignore the occurrence indicator when classifying the next token
                    t.currentToken = Token.RPAR;
                    NextToken();
                    break;
                default:
                    occurrenceFlag = StaticProperty.EXACTLY_ONE;
                    break;
            }

            return occurrenceFlag;
        }

        public virtual Types.ItemType ParseItemType()
        {
            // Same .NET stack-overflow guard as ParseExprSingle: the item-type grammar is a second
            // recursive descent (parenthesized/function/array/map types nest through here), so a
            // pathologically deep type like `((((item()))))` would otherwise crash the process.
            if (++expressionDepth > MAX_EXPRESSION_NESTING)
            {
                expressionDepth--;
                Grumble("Item type is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
            }

            try
            {
                Types.ItemType extended = parserExtension.ParseExtendedItemType(this);
                return extended == null ? ParseSimpleItemType() : extended;
            }
            finally
            {
                expressionDepth--;
            }
        }

        private Types.ItemType ParseSimpleItemType()
        {
            Types.ItemType primaryType;
            if (t.currentToken == Token.LPAR)
            {
                primaryType = ParseParenthesizedItemType(); //nextToken();
            }
            else if (t.currentToken == Token.NAME)
            {
                primaryType = GetPlainType(t.currentTokenValue);
                NextToken();
            }
            else if (t.currentToken == Token.KEYWORD_LBRA || t.currentToken == Token.FUNCTION)
            {

                // Which includes things such as "map" and "array"
                switch (t.currentTokenValue)
                {
                    case "item":
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        primaryType = AnyItemType.GetInstance();
                        break;
                    case "function":
                        {
                            CheckLanguageVersion30();
                            AnnotationList annotations = AnnotationList.EMPTY;
                            primaryType = ParseFunctionItemType(annotations);
                            break;
                        }

                    case "fn":
                        {
                            CheckLanguageVersion40();
                            AnnotationList annotations = AnnotationList.EMPTY;
                            primaryType = ParseFunctionItemType(annotations);
                            break;
                        }

                    case "map":
                        primaryType = ParseMapItemType();
                        break;
                    case "array":
                        primaryType = ParseArrayItemType();
                        break;
                    case "record":
                    case "tuple":
                        primaryType = ParseRecordTest(this);
                        break;
                    case "atomic":

                        // Allowed only in patterns, not in item types??
                        // TODO: not in spec, drop this
                        CheckLanguageVersion40();
                        Warning("The pattern syntax atomic(typename) is likely to be dropped from the 4.0 specification. Use type(typename) instead.", DAXonErrorCode.SXWN9000);
                        NextToken();
                        Expect(Token.NAME);
                        StructuredQName typeName = GetQNameParser().Parse(t.currentTokenValue, NamespaceUri.SCHEMA);
                        primaryType = GetPlainType(typeName);
                        if (!(primaryType is IAtomicType))
                        {
                            Grumble("Type " + t.currentTokenValue + " exists, but is not atomic");
                        }

                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        break;
                    case "union":
                        primaryType = ParseUnionType();
                        break;
                    case "enum":
                        primaryType = ParseEnumType();
                        break;
                    case "empty-sequence":
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        primaryType = ErrorType.GetInstance();
                        break;
                    case "type":
                        CheckLanguageVersion40();
                        NextToken();
                        if (t.currentToken == Token.NAME)
                        {
                            StructuredQName qName = GetQNameParser().Parse(t.currentTokenValue, NamespaceUri.NULL);
                            Types.ItemType realType = GetStaticContext().ResolveTypeAlias(qName);
                            if (realType != null)
                            {
                                NextToken();
                                Expect(Token.RPAR);
                                NextToken();
                                return realType;
                            }
                        }

                        if (language != ParsedLanguage.XSLT_PATTERN)
                        {
                            Grumble("In an XPath expression (as distinct from an XSLT pattern), type(N) must refer to a named item type");
                        }

                        Types.ItemType it = ParseItemType();
                        Expect(Token.RPAR);
                        NextToken();
                        return it;
                    default:
                        primaryType = ParseKindTest();
                        break;
                }
            }
            else if (t.currentToken == Token.PERCENT)
            {
                AnnotationList annotations = ParseAnnotationsList();
                if (t.currentTokenValue.Equals("function"))
                {
                    primaryType = ParseFunctionItemType(annotations);
                }
                else
                {
                    Grumble("Expected 'function' to follow annotation assertions, found " + Token.tokens[t.currentToken]);
                    return null;
                }
            }
            else if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.PREFIX)
            {
                string tokv = t.currentTokenValue;
                NextToken();
                return MakeNamespaceTest(Types.Type.ELEMENT, tokv);
            }
            else if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.SUFFIX)
            {
                NextToken();
                Expect(Token.NAME);
                string tokv = t.currentTokenValue;
                NextToken();
                return MakeLocalNameTest(Types.Type.ELEMENT, tokv);
            }
            else if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.AT)
            {
                NextToken();
                if (t.currentToken == Token.PREFIX)
                {
                    string tokv = t.currentTokenValue;
                    NextToken();
                    return MakeNamespaceTest(Types.Type.ATTRIBUTE, tokv);
                }
                else if (t.currentToken == Token.SUFFIX)
                {
                    NextToken();
                    Expect(Token.NAME);
                    string tokv = t.currentTokenValue;
                    NextToken();
                    return MakeLocalNameTest(Types.Type.ATTRIBUTE, tokv);
                }
                else
                {
                    Grumble("Expected NodeTest after '@'");
                    return BuiltInAtomicType.ANY_ATOMIC;
                }
            }
            else
            {
                Grumble("Expected type name in SequenceType, found " + Token.tokens[t.currentToken]);
                return BuiltInAtomicType.ANY_ATOMIC;
            }

            return primaryType;
        }

        private Types.ItemType ParseRecordTest(XPathParser p)
        {

            // The initial "record(" has been read
            CheckLanguageVersion40();
            Tokenizer t = p.GetTokenizer();
            p.NextToken();
            IList<string> fieldNames = new List<string>(6);
            IList<string> optionalFieldNames = new List<string>(6);
            IList<Values.SequenceType> fieldTypes = new List<Values.SequenceType>(6);
            bool extensible = false;
            RecordTest recordTest = new RecordTest();
            while (true)
            {
                string name;
                if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
                {
                    extensible = true;
                    p.NextToken();
                    p.Expect(Token.RPAR);
                    break;
                }

                if (t.currentToken == Token.NAME)
                {
                    name = t.currentTokenValue;
                    if (!NameChecker.IsValidNCName(name))
                    {
                        p.Grumble(Err.Wrap(name) + " is not a valid NCName");
                    }
                }
                else if (t.currentToken == Token.STRING_LITERAL)
                {
                    name = t.currentTokenValue;
                }
                else
                {
                    p.Grumble("Name of field in tuple must be either an NCName or a quoted string literal");
                    name = "dummy";
                }

                if (fieldNames.Contains(name))
                {
                    p.Grumble("Duplicate field name (" + name + ")");
                    name = "dummy";
                }

                fieldNames.Add(name);
                p.NextToken();
                if (t.currentToken == Token.QMARK)
                {
                    optionalFieldNames.Add(name);
                    p.NextToken();
                }

                Values.SequenceType arg = Values.SequenceType.ANY_SEQUENCE;
                if (t.currentToken == Token.AS)
                {
                    p.NextToken();
                    if (t.currentToken == Token.DOTDOT)
                    {

                        // self-reference
                        p.NextToken();
                        int occ = ParseOccurrenceIndicator();
                        arg = Values.SequenceType.MakeSequenceType((Types.ItemType)(new SelfReferenceRecordTest(recordTest)), occ);
                        if (!Cardinality.AllowsZero(occ) && !optionalFieldNames.Contains(name))
                        {
                            throw new XPathException("A self-referencing field in a record type must be emptiable or optional", "XPST0140");
                        }
                    }
                    else
                    {
                        arg = p.ParseSequenceType();
                    }
                }

                fieldTypes.Add(arg);
                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    p.NextToken();
                }
                else
                {
                    p.Grumble("Expected ',' or ')' after field in RecordTest, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            p.NextToken();
            recordTest.SetDetails(fieldNames, fieldTypes, optionalFieldNames, extensible);
            return recordTest;
        }

        public virtual Types.ItemType ParseUnionType()
        {

            // The initial "union(" has been read
            CheckLanguageVersion40();
            NextToken();
            IList<IAtomicType> memberTypes = new List<IAtomicType>(6);
            while (true)
            {
                if (t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("enum"))
                {
                    EnumerationType type = ParseEnumType();
                    memberTypes.Add(type);
                }
                else
                {
                    Expect(Token.NAME);
                    if (scanOnly)
                    {
                        memberTypes.Add(BuiltInAtomicType.STRING);
                    }
                    else
                    {
                        StructuredQName member = GetQNameParser().Parse(t.currentTokenValue, GetStaticContext().GetDefaultElementNamespace());
                        Types.ItemType type = GetPlainType(member);
                        if (type is IAtomicType)
                        {
                            memberTypes.Add((IAtomicType)type);
                        }
                        else if (type is IPlainType)
                        {
                            foreach (IPlainType pt in ((IUnionType)type).PlainMemberTypes)
                            {
                                if (pt is IAtomicType)
                                {
                                    memberTypes.Add((IAtomicType)pt);
                                }
                                else
                                {
                                    Grumble("Union type " + type + " has a non-atomic member type " + pt);
                                }
                            }
                        }
                        else
                        {
                            Grumble("Type " + t.currentTokenValue + " exists, but is not atomic");
                        }
                    }

                    NextToken();
                }

                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    Grumble("Expected ',' or ')' after member name in union type, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            NextToken();
            return new LocalUnionType(memberTypes);
        }

        public virtual EnumerationType ParseEnumType()
        {

            // The initial "enum(" has been read
            CheckLanguageVersion40();
            NextToken();
            HashSet<string> values = new HashSet<string>(6);
            while (true)
            {
                Expect(Token.STRING_LITERAL);
                values.Add(t.currentTokenValue);
                NextToken();
                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    Grumble("Expected ',' or ')' after string literal in enum type, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            NextToken();
            return new EnumerationType(values);
        }

        protected virtual Types.ItemType ParseFunctionItemType(AnnotationList annotations)
        {
            NextToken();
            IList<Values.SequenceType> argTypes = new List<Values.SequenceType>(3);
            Values.SequenceType resultType;
            if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
            {

                // Allow both to be safe
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                if (annotations.IsEmpty())
                {
                    return AnyFunctionType.GetInstance();
                }
                else
                {
                    return (Types.ItemType)new AnyFunctionTypeWithAssertions(annotations, GetStaticContext().GetConfiguration());
                }
            }
            else
            {
                while (t.currentToken != Token.RPAR)
                {
                    Values.SequenceType arg = ParseSequenceType();
                    argTypes.Add(arg);
                    if (t.currentToken == Token.RPAR)
                    {
                        break;
                    }
                    else if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else
                    {
                        Grumble("Expected ',' or ')' after function argument type, found '" + Token.tokens[t.currentToken] + '\'');
                    }
                }

                NextToken();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    resultType = ParseSequenceType();
                    Values.SequenceType[] argArray = new Values.SequenceType[argTypes.Count];
                    argArray = argTypes.ToArray(argArray);
                    return new SpecificFunctionType(argArray, resultType, annotations);
                }
                else if (!argTypes.IsEmpty())
                {
                    Grumble("Result type must be given if an argument type is given: expected 'as (type)'");
                    return null;
                }
                else
                {
                    Grumble("function() is no longer allowed for a general function type: must be function(*)");
                    return null; // in the new syntax adopted on 2009-09-22, this case is an error
                }
            }
        }

        protected virtual Types.ItemType ParseMapItemType()
        {
            CheckMapExtensions();
            Tokenizer t = GetTokenizer();
            NextToken();
            if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
            {

                // Allow both to be safe
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                return MapType.ANY_MAP_TYPE;
            }
            else
            {
                Types.ItemType keyType = ParseItemType();
                Expect(Token.COMMA);
                NextToken();
                Values.SequenceType valueType = ParseSequenceType();
                Expect(Token.RPAR);
                NextToken();
                if (!(keyType is IPlainType))
                {
                    Grumble("Key type of a map must be an atomic or pure union type: found " + keyType);
                    return null;
                }

                return new MapType((IPlainType)keyType, valueType);
            }
        }

        protected virtual Types.ItemType ParseArrayItemType()
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            NextToken();
            if (t.currentToken == Token.STAR || t.currentToken == Token.MULT)
            {

                // Allow both to be safe
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                return ArrayItemType.ANY_ARRAY_TYPE;
            }
            else
            {
                Values.SequenceType memberType = ParseSequenceType();
                Expect(Token.RPAR);
                NextToken();
                return new ArrayItemType(memberType);
            }
        }

        private Types.ItemType ParseParenthesizedItemType()
        {
            if (!allowXPath30Syntax)
            {
                Grumble("Parenthesized item types require 3.0 to be enabled");
            }

            NextToken();
            Types.ItemType primaryType = ParseItemType();
            while (primaryType is NodeTest && language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken != Token.RPAR)
            {
                switch (t.currentToken)
                {
                    case Token.UNION:
                    case Token.EXCEPT:
                    case Token.INTERSECT:
                        int op = t.currentToken;
                        NextToken();
                        primaryType = new CombinedNodeTest((NodeTest)primaryType, op, (NodeTest)ParseItemType());
                        break;
                }
            }

            Expect(Token.RPAR);
            NextToken();
            return primaryType;
        }

        private Expression ParseUnaryExpression()
        {
            Expression exp;
            switch (t.currentToken)
            {
                case Token.MINUS:
                    {
                        // Direct recursion bypassing the ParseExprSingle guard: an unguarded ----...-1
                        // sign chain overflows the uncatchable .NET stack (~3M signs). Same idiom:
                        // increment before the try so the finally pairs only with a successful entry.
                        if (++expressionDepth > MAX_EXPRESSION_NESTING)
                        {
                            expressionDepth--;
                            Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
                        }

                        try
                        {
                            NextToken();
                            Expression operand = ParseUnaryExpression();
                            exp = MakeUnaryExpression(Token.NEGATE, operand);
                        }
                        finally
                        {
                            expressionDepth--;
                        }

                        break;
                    }

                case Token.PLUS:
                    {
                        // Same direct-recursion guard as MINUS above.
                        if (++expressionDepth > MAX_EXPRESSION_NESTING)
                        {
                            expressionDepth--;
                            Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
                        }

                        try
                        {
                            NextToken();

                            // Unary plus: can't ignore it completely, it might be a type error, or it might
                            // force conversion to a number which would affect operations such as "=".
                            Expression operand = ParseUnaryExpression();
                            exp = MakeUnaryExpression(Token.PLUS, operand);
                        }
                        finally
                        {
                            expressionDepth--;
                        }

                        break;
                    }

                case Token.VALIDATE:
                case Token.VALIDATE_STRICT:
                case Token.VALIDATE_LAX:
                case Token.VALIDATE_TYPE:
                    exp = ParseValidateExpression();
                    break;
                case Token.PRAGMA:
                    exp = ParseExtensionExpression();
                    break;
                case Token.KEYWORD_CURLY:
                    if (t.currentTokenValue.Equals("validate"))
                    {
                        exp = ParseValidateExpression();
                        break;
                    }

                    goto default;
                default:
                    exp = ParseSimpleMappingExpression();
                    break;
            }

            SetLocation(exp);
            return exp;
        }

        private Expression MakeUnaryExpression(int @operator, Expression operand)
        {
            if (Literal.IsAtomic(operand))
            {

                // very early evaluation of expressions like "-1", so they are treated as numeric literals
                AtomicValue val = (AtomicValue)((Literal)operand).GroundedValue;
                if (val is NumericValue)
                {
                    if (env.IsInBackwardsCompatibleMode())
                    {
                        val = new DoubleValue(((NumericValue)val).GetDoubleValue());
                    }

                    AtomicValue value = @operator == Token.NEGATE ? ((NumericValue)val).Negate() : (NumericValue)val;
                    return Literal.MakeLiteral(value);
                }
            }

            return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(Literal.MakeLiteral(Int64Value.ZERO), @operator, operand);
        }

        protected virtual bool AtStartOfRelativePath()
        {
            switch (t.currentToken)
            {
                case Token.AXIS:
                case Token.AT:
                case Token.NAME:
                case Token.PREFIX:
                case Token.SUFFIX:
                case Token.STAR:
                case Token.KEYWORD_LBRA:
                case Token.DOT:
                case Token.DOTDOT:
                case Token.FUNCTION:
                case Token.STRING_LITERAL:
                case Token.NUMBER:
                case Token.HEX_INTEGER:
                case Token.BINARY_INTEGER:
                case Token.LPAR:
                case Token.DOLLAR:
                case Token.PRAGMA:
                case Token.ELEMENT_QNAME:
                case Token.ATTRIBUTE_QNAME:
                case Token.PI_QNAME:
                case Token.NAMESPACE_QNAME:
                case Token.NAMED_FUNCTION_REF:
                case Token.LSQB:
                    return true;
                case Token.KEYWORD_CURLY:
                    return t.currentTokenValue.Equals("ordered") || t.currentTokenValue.Equals("unordered") || t.currentTokenValue.Equals("map") || t.currentTokenValue.Equals("array");
                default:
                    return false;
            }
        }

        protected virtual bool DisallowedAtStartOfRelativePath()
        {
            switch (t.currentToken)
            {
                case Token.CAST_AS:
                case Token.CASTABLE_AS:
                case Token.INSTANCE_OF:
                case Token.TREAT_AS:
                    return true;
                default:
                    return false;
            }
        }

        protected virtual Expression ParsePathExpression()
        {
            int offset = t.currentTokenStartOffset;
            switch (t.currentToken)
            {
                case Token.SLASH:
                    NextToken();
                    RootExpression start = new RootExpression();
                    SetLocation(start);
                    if (DisallowedAtStartOfRelativePath())
                    {
                        Grumble("Operator '" + Token.tokens[t.currentToken] + "' is not allowed after '/'");
                    }

                    if (AtStartOfRelativePath())
                    {
                        Expression path = ParseRemainingPath(start);
                        SetLocation(path, offset);
                        return path;
                    }
                    else
                    {
                        return start;
                    }

                case Token.SLASH_SLASH:
                    NextToken();
                    RootExpression start2 = new RootExpression();
                    SetLocation(start2, offset);
                    AxisExpression axisExp = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, null);
                    SetLocation(axisExp, offset);
                    Expression slashExp = ExpressionTool.MakePathExpression(start2, axisExp);
                    SetLocation(slashExp, offset);
                    Expression exp = ParseRemainingPath(slashExp);
                    SetLocation(exp, offset);
                    return exp;
                default:
                    if (t.currentToken == Token.NAME && (t.currentTokenValue.Equals("true") || t.currentTokenValue.Equals("false")))
                    {
                        Warning("The expression is looking for a child element named '" + t.currentTokenValue + "' - perhaps " + t.currentTokenValue + "() was intended? To avoid this warning, use child::" + t.currentTokenValue + " or ./" + t.currentTokenValue + ".", DAXonErrorCode.SXWN9040);
                    }

                    if (t.currentToken == Token.NAME && t.GetBinaryOp(t.currentTokenValue) != Token.UNKNOWN && language != ParsedLanguage.XSLT_PATTERN && (offset > 0 || t.PeekAhead() != Token.EOF))
                    {
                        string s = t.currentTokenValue;
                        Warning("The keyword '" + s + "' in this context means 'child::" + s + "'. If this was intended, use 'child::" + s + "' or './" + s + "' to avoid this warning.", DAXonErrorCode.SXWN9040);
                    }

                    return ParseRelativePath();
            }
        }

        protected virtual Expression ParseSimpleMappingExpression()
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = ParsePathExpression();
            int chain = 0;
            while (t.currentToken == Token.BANG)
            {
                CheckIterativeDepth(++chain);
                if (!allowXPath30Syntax)
                {
                    Grumble("XPath '!' operator requires XPath 3.0 to be enabled");
                }

                NextToken();
                Expression next = ParsePathExpression();
                exp = new ForEach(exp, next);
                SetLocation(exp, offset);
            }

            return exp;
        }

        protected virtual Expression ParseRelativePath()
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = ParseStepExpression(language == ParsedLanguage.XSLT_PATTERN);
            int chain = 0;
            while (t.currentToken == Token.SLASH || t.currentToken == Token.SLASH_SLASH)
            {
                CheckIterativeDepth(++chain);
                int op = t.currentToken;
                NextToken();
                Expression next = ParseStepExpression(false);
                if (op == Token.SLASH)
                {

                    //return new RawSlashExpression(start, step);
                    exp = new HomogeneityChecker(new SlashExpression(exp, next));
                } /* (op == Token.SLASH_SLASH)*/
                else
                {

                    // add implicit descendant-or-self.node() step
                    AxisExpression ae = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, null);
                    SetLocation(ae, offset);
                    Expression one = ExpressionTool.MakePathExpression(exp, ae);
                    SetLocation(one, offset);
                    exp = ExpressionTool.MakePathExpression(one, next);
                    exp = new HomogeneityChecker(exp);
                }

                SetLocation(exp, offset);
            }

            return exp;
        }

        protected virtual Expression ParseRemainingPath(Expression start)
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = start;
            int op = Token.SLASH;
            int chain = 0;
            while (true)
            {
                CheckIterativeDepth(++chain);
                Expression next = ParseStepExpression(false);
                if (op == Token.SLASH)
                {

                    //return new RawSlashExpression(start, step);
                    exp = new HomogeneityChecker(new SlashExpression(exp, next));
                }
                else if (op == Token.SLASH_SLASH)
                {

                    // add implicit descendant-or-self.node() step
                    AxisExpression descOrSelf = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, null);
                    SetLocation(descOrSelf);
                    Expression step = ExpressionTool.MakePathExpression(descOrSelf, next);
                    SetLocation(step);
                    exp = ExpressionTool.MakePathExpression(exp, step);
                    exp = new HomogeneityChecker(exp);
                } /*if (op == Token.BANG)*/
                else
                {
                    if (!allowXPath30Syntax)
                    {
                        Grumble("XPath '!' operator requires XPath 3.0 to be enabled");
                    }

                    exp = new ForEach(exp, next);
                }

                SetLocation(exp, offset);
                op = t.currentToken;
                if (op != Token.SLASH && op != Token.SLASH_SLASH && op != Token.BANG)
                {
                    break;
                }

                NextToken();
            }

            return exp;
        }

        protected virtual Expression ParseStepExpression(bool firstInPattern)
        {
            Expression step = ParseBasicStep(firstInPattern);

            // When the filter is applied to an Axis step, the nodes are considered in
            // axis order. In all other cases they are considered in document order
            bool reverse = (step is AxisExpression) && !AxisInfo.isForwards[((AxisExpression)step).Axis];
            int chain = 0;
            while (true)
            {
                CheckIterativeDepth(++chain);
                if (t.currentToken == Token.LSQB)
                {
                    step = ParsePredicate(step);
                }
                else if (t.currentToken == Token.LPAR)
                {

                    // dynamic function call (XQuery 3.0/XPath 3.0 syntax)
                    step = ParseDynamicFunctionCall(step, null);
                    SetLocation(step);
                }
                else if (t.currentToken == Token.QMARK)
                {
                    step = ParseLookup(step);
                    SetLocation(step);
                }
                else
                {
                    break;
                }
            }

            if (reverse)
            {

                // An AxisExpression such as preceding-sibling.x delivers nodes in axis
                // order, so that positional predicate like preceding-sibling.x[1] work
                // correctly. To satisfy the XPath semantics we turn preceding-sibling.x
                // into reverse(preceding-sibling.x), and preceding-sibling.x[3] into
                // reverse(preceding-sibling.x[3]). The call on reverse() will be eliminated
                // later in the case where the predicate selects a singleton.
                RetainedStaticContext rsc = env.MakeRetainedStaticContext();
                step = SystemFunction.MakeCall("reverse", rsc, step);
            }

            return step;
        }

        protected virtual Expression ParsePredicate(Expression step)
        {
            NextToken();
            Expression predicate = ParsePredicate();
            if (Literal.IsConstantZero(predicate))
            {
                Warning("Positions are numbered from one; the predicate [0] selects nothing", DAXonErrorCode.SXWN9046);
            }

            Expect(Token.RSQB);
            NextToken();
            step = new FilterExpression(step, predicate);
            SetLocation(step);
            return step;
        }

        protected virtual Expression ParseArrowPostfix(Expression lhs)
        {
            CheckLanguageVersion31();
            NextToken();
            int token = GetTokenizer().currentToken;
            if (token == Token.NAME || token == Token.FUNCTION)
            {
                return ParseFunctionCall(lhs);
            }
            else if (token == Token.DOLLAR)
            {
                int offset = t.currentTokenStartOffset;
                StructuredQName varName = ParseVariableName();
                Expression var = ResolveVariableReference(offset, varName);
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(var, lhs);
            }
            else if (token == Token.LPAR)
            {
                Expression var = ParseParenthesizedExpression();
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(var, lhs);
            }
            else if (token == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("function"))
            {
                Expression fn = ParseInlineFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(fn, lhs);
            }
            else if (token == Token.KEYWORD_CURLY && (t.currentTokenValue.Equals("function") || t.currentTokenValue.Equals("fn")))
            {
                Expression fn = ParseFocusFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(fn, lhs);
            }
            else
            {
                Grumble("Unexpected " + Token.tokens[token] + " after '=>'");
                return null;
            }
        }

        protected virtual Expression ParseMappingArrowPostfix(Expression lhs)
        {
            CheckLanguageVersion40();
            NextToken();
            ForExpression forExpr = new ForExpression();
            forExpr.Sequence = lhs;
            StructuredQName varName = new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "" + lhs.GetHashCode());
            forExpr.SetVariableQName(varName);
            VariableReference varRef = new LocalVariableReference(forExpr);
            int token = GetTokenizer().currentToken;
            Expression rhs;
            if (token == Token.NAME || token == Token.FUNCTION)
            {
                rhs = ParseFunctionCall(varRef);
            }
            else if (token == Token.DOLLAR)
            {
                int offset = t.currentTokenStartOffset;
                StructuredQName variableName = ParseVariableName();
                Expression var = ResolveVariableReference(offset, variableName);
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(var, varRef);
            }
            else if (token == Token.LPAR)
            {
                Expression var = ParseParenthesizedExpression();
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(var, varRef);
            }
            else if (token == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("function"))
            {
                Expression fn = ParseInlineFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(fn, varRef);
            }
            else if (token == Token.KEYWORD_CURLY && (t.currentTokenValue.Equals("function") || t.currentTokenValue.Equals("fn")))
            {
                Expression fn = ParseFocusFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(fn, varRef);
            }
            else
            {
                Grumble("Unexpected " + Token.tokens[token] + " after '=!>'");
                return null;
            }

            forExpr.SetAction(rhs);
            return forExpr;
        }

        protected virtual Expression ParsePredicate()
        {
            return ParseExpression();
        }

        protected virtual bool IsReservedInQuery(NamespaceUri uri)
        {
            return NamespaceUri.IsReservedInQuery31(uri);
        }

        protected virtual Expression ParseBasicStep(bool firstInPattern)
        {
            switch (t.currentToken)
            {
                case Token.DOLLAR:
                    int offset = t.currentTokenStartOffset;
                    StructuredQName variableName = ParseVariableName();
                    return ResolveVariableReference(offset, variableName);
                case Token.LPAR:
                    if (allowXPath40Syntax && t.ThereMightBeAnArrowAhead() && (t.PeekAhead() == Token.DOLLAR || t.PeekAhead() == Token.RPAR))
                    {

                        // Possible lambda expression.
                        Tokenizer checkpoint = new Tokenizer();
                        t.CopyTo(checkpoint);
                        IList<StructuredQName> lambdaParams = ParseLambdaParams();
                        if (lambdaParams == null)
                        {

                            // backtrack and resume a normal parse
                            checkpoint.CopyTo(t);
                        }
                        else
                        {

                            //nextToken();
                            IList<UserFunctionParameter> @params = new List<UserFunctionParameter>(lambdaParams.Count);
                            int slotNumber = 0;
                            foreach (StructuredQName paramName in lambdaParams)
                            {
                                UserFunctionParameter arg = new UserFunctionParameter();
                                arg.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                                arg.SetVariableQName(paramName);
                                arg.SetSlotNumber(slotNumber++);
                                @params.Add(arg);
                            }

                            return ParseInlineFunctionBody(AnnotationList.EMPTY, @params, Values.SequenceType.ANY_SEQUENCE);
                        }
                    }

                    return ParseParenthesizedExpression();
                case Token.LSQB:
                    return ParseArraySquareConstructor();
                case Token.STRING_LITERAL:
                    return ParseStringLiteral(true);
                case Token.STRING_LITERAL_BACKTICKED:
                    return ParseBackTickedStringLiteral();
                case Token.STRING_CONSTRUCTOR_INITIAL:
                    return ParseStringConstructor();
                case Token.BACKTICK:
                    CheckLanguageVersion40();
                    return ParseStringTemplate();
                case Token.NUMBER:
                    return ParseNumericLiteral(true);
                case Token.HEX_INTEGER:
                    return ParseHexLiteral(true);
                case Token.BINARY_INTEGER:
                    return ParseBinaryLiteral(true);
                case Token.FUNCTION:
                    return ParseFunctionCall(null);
                case Token.QMARK:
                    return ParseLookup(new ContextItemExpression());
                case Token.DOT:
                    NextToken();
                    Expression cie = new ContextItemExpression();
                    SetLocation(cie);
                    return cie;
                case Token.DOTDOT:
                    NextToken();
                    Expression pne = new AxisExpression(AxisInfo.PARENT, null);
                    SetLocation(pne);
                    return pne;
                case Token.PERCENT:
                    {
                        AnnotationList annotations = ParseAnnotationsList();
                        if (t.currentToken == Token.THIN_ARROW)
                        {
                            CheckLanguageVersion40();
                            NextToken();
                            annotations.Check(env.GetConfiguration(), "IF");
                            if (t.currentToken == Token.LPAR)
                            {
                                return ParseInlineFunction(annotations);
                            }
                            else if (t.currentToken == Token.LCURLY)
                            {
                                return ParseFocusFunction(annotations);
                            }
                            else
                            {
                                Grumble("Expected '(' or '{' after '->'");
                            }
                        }

                        if (!(t.currentTokenValue.Equals("function") || t.currentTokenValue.Equals("fn")))
                        {
                            Grumble("Expected 'function' to follow the annotation assertion");
                        }

                        annotations.Check(env.GetConfiguration(), "IF");
                        if (t.currentToken == Token.KEYWORD_CURLY)
                        {
                            return ParseFocusFunction(annotations);
                        }
                        else
                        {
                            return ParseInlineFunction(annotations);
                        }
                    }

                case Token.THIN_ARROW:
                    {
                        CheckLanguageVersion40();
                        NextToken();
                        if (t.currentToken == Token.LPAR)
                        {
                            AnnotationList annotations = AnnotationList.EMPTY;
                            return ParseInlineFunction(annotations);
                        }
                        else if (t.currentToken == Token.LCURLY)
                        {
                            AnnotationList annotations = AnnotationList.EMPTY;
                            return ParseFocusFunction(annotations);
                        }
                        else
                        {
                            Grumble("Expected '(' or '{' after '->'");
                        }

                        break;
                    }

                case Token.KEYWORD_LBRA:
                    if (t.currentTokenValue.Equals("function") || (t.currentTokenValue.Equals("fn")))
                    {
                        AnnotationList annotations = AnnotationList.EMPTY;
                        return ParseInlineFunction(annotations);
                    }

                    goto case Token.NAME;
                case Token.NAME:
                case Token.PREFIX:
                case Token.SUFFIX:
                case Token.STAR:
                    byte defaultAxis = AxisInfo.CHILD;
                    if (t.currentToken == Token.KEYWORD_LBRA && (t.currentTokenValue.Equals("attribute") || t.currentTokenValue.Equals("schema-attribute")))
                    {
                        defaultAxis = AxisInfo.ATTRIBUTE;
                    }
                    else if (t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("namespace-node"))
                    {
                        defaultAxis = AxisInfo.NAMESPACE;
                        TestPermittedAxis(AxisInfo.NAMESPACE, "XQST0134");
                    }
                    else if (firstInPattern && t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("document-node"))
                    {
                        defaultAxis = AxisInfo.SELF;
                    }

                    NodeTest test = ParseNodeTest(Types.Type.ELEMENT);
                    if (test is AnyNodeTest)
                    {

                        // handles patterns of the form match="node()"
                        if (defaultAxis == AxisInfo.CHILD)
                        {
                            test = MultipleNodeKindTest.CHILD_NODE;
                        }
                        else
                        {
                            test = NodeKindTest.ATTRIBUTE;
                        }
                    }

                    AxisExpression ae = new AxisExpression(defaultAxis, test);
                    SetLocation(ae);
                    return ae;
                case Token.AT:
                    NextToken();
                    switch (t.currentToken)
                    {
                        case Token.NAME:
                        case Token.PREFIX:
                        case Token.SUFFIX:
                        case Token.STAR:
                        case Token.KEYWORD_LBRA:
                        case Token.LPAR:
                            AxisExpression ae2 = new AxisExpression(AxisInfo.ATTRIBUTE, ParseNodeTest(Types.Type.ATTRIBUTE));
                            SetLocation(ae2);
                            return ae2;
                        default:
                            Grumble("@ must be followed by a NodeTest");
                            break;
                    }

                    break;
                case Token.AXIS:
                    int axis;
                    try
                    {
                        axis = AxisInfo.GetAxisNumber(t.currentTokenValue);
                    }
                    catch (XPathException err)
                    {
                        Grumble(err.GetMessage());
                        axis = AxisInfo.CHILD; // error recovery
                    }

                    TestPermittedAxis(axis, "XPST0003");
                    short principalNodeType = AxisInfo.principalNodeType[axis];
                    NextToken();
                    switch (t.currentToken)
                    {
                        case Token.NAME:
                        case Token.PREFIX:
                        case Token.SUFFIX:
                        case Token.STAR:
                        case Token.KEYWORD_LBRA:
                        case Token.LPAR:
                            Expression ax = new AxisExpression(axis, ParseNodeTest(principalNodeType));
                            SetLocation(ax);
                            return ax;
                        default:
                            Grumble("Unexpected token " + CurrentTokenDisplay() + " after axis name");
                            break;
                    }

                    break;
                case Token.KEYWORD_CURLY:
                    switch (t.currentTokenValue)
                    {
                        case "map":
                            return ParseMapExpression();
                        case "array":
                            return ParseArrayCurlyConstructor();
                        case "function":
                        case "fn":
                            return ParseFocusFunction(null);
                    }

                    goto case Token.ELEMENT_QNAME;
                case Token.ELEMENT_QNAME:
                case Token.ATTRIBUTE_QNAME:
                case Token.NAMESPACE_QNAME:
                case Token.PI_QNAME:
                case Token.TAG:
                    return ParseConstructor();
                case Token.NAMED_FUNCTION_REF:
                    return ParseNamedFunctionReference();
                default:
                    Grumble("Unexpected token " + CurrentTokenDisplay() + " at start of expression");
                    break;
            }

            return new ErrorExpression();
        }

        public virtual Expression ParseParenthesizedExpression()
        {
            NextToken();
            if (t.currentToken == Token.RPAR)
            {
                NextToken();
                return MakeTracer(Literal.MakeEmptySequence(), null);
            }

            Expression seq = ParseExpression();
            Expect(Token.RPAR);
            NextToken();
            return seq;
        }

        public virtual IList<StructuredQName> ParseLambdaParams()
        {
            IList<StructuredQName> result = new List<StructuredQName>(4);
            NextToken();
            if (t.currentToken == Token.RPAR)
            {
                NextToken();
                if (t.currentToken == Token.THIN_ARROW)
                {
                    NextToken();
                    return result;
                }
                else
                {
                    return null;
                }
            }

            while (true)
            {
                if (t.currentToken != Token.DOLLAR)
                {
                    return null;
                }

                NextToken();
                if (t.currentToken == Token.NAME)
                {
                    result.Add(MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL));
                }
                else
                {
                    return null;
                }

                NextToken();
                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else if (t.currentToken == Token.RPAR)
                {
                    NextToken();
                    if (t.currentToken == Token.THIN_ARROW)
                    {
                        NextToken();
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        protected virtual void TestPermittedAxis(int axis, string errorCode)
        {
            if (axis == AxisInfo.PRECEDING_OR_ANCESTOR)
            {
                Grumble("The preceding-or-ancestor axis is for internal use only", errorCode);
            }
        }

        public virtual Expression ParseNumericLiteral(bool traceable)
        {
            int offset = t.currentTokenStartOffset;
            NumericValue number = NumericValue.ParseNumber(t.currentTokenValue);
            if (number.IsNaN())
            {
                Grumble("Invalid numeric literal " + Err.Wrap(t.currentTokenValue, Err.VALUE));
            }

            NextToken();
            Literal lit = Literal.MakeLiteral(number);
            SetLocation(lit, offset);

            return traceable ? MakeTracer(lit, null) : lit;
        }

        public virtual Expression ParseHexLiteral(bool traceable)
        {
            try
            {
                int offset = t.currentTokenStartOffset;
                IntegerValue val;
                if (t.currentTokenValue.Length < 16)
                {
                    if ((t.currentTokenValue.Length == 0))
                    {

                        // Handled specially because .NET code otherwise crashes
                        Grumble("Empty hex literal");
                    }

                    long parsed = Convert.ToInt64(t.currentTokenValue, 16);
                    val = new Int64Value(parsed);
                }
                else
                {
                    BigInteger big = BigIntegers.FromString(t.currentTokenValue, 16);
                    val = new BigIntegerValue(big);
                }

                NextToken();
                Literal lit = Literal.MakeLiteral(val);
                SetLocation(lit, offset);
                return traceable ? MakeTracer(lit, null) : lit;
            }
            catch (FormatException e)
            {
                Grumble("Invalid hex literal");
                return null;
            }
        }

        public virtual Expression ParseBinaryLiteral(bool traceable)
        {
            try
            {
                int offset = t.currentTokenStartOffset;
                IntegerValue val;
                if (t.currentTokenValue.Length < 64)
                {
                    if ((t.currentTokenValue.Length == 0))
                    {

                        // Handled specially because .NET code otherwise crashes
                        Grumble("Empty binary literal");
                    }

                    long parsed = BinaryStringToLong(t.currentTokenValue);
                    val = new Int64Value(parsed);
                }
                else
                {
                    BigInteger big = BigIntegers.FromString(t.currentTokenValue, 2);
                    val = new BigIntegerValue(big);
                }

                NextToken();
                Literal lit = Literal.MakeLiteral(val);
                SetLocation(lit, offset);
                return traceable ? MakeTracer(lit, null) : lit;
            }
            catch (FormatException e)
            {
                Grumble("Invalid binary literal");
                return null;
            }
        }

        private long BinaryStringToLong(string input)
        {
            return Convert.ToInt64(input, 2);
        }

        protected virtual Expression ParseStringLiteral(bool traceable)
        {
            Literal literal = MakeStringLiteral(t.currentTokenValue, true);
            NextToken();
            return traceable ? MakeTracer(literal, null) : literal;
        }

        protected virtual Expression ParseBackTickedStringLiteral()
        {
            Literal literal = MakeStringLiteral(t.currentTokenValue, false);
            NextToken();
            return MakeTracer(literal, null);
        }

        protected virtual Expression ParseStringConstructor()
        {
            Grumble("String constructor expressions are allowed only in XQuery");
            return null;
        }

        public virtual Expression ParseStringTemplate()
        {
            int offset = t.inputOffset;

            // we're reading raw characters
            //t.nextChar(); // lose this one, it's the initial backtick
            IList<Expression> components = new List<Expression>();
            StringBuilder currentPart = new StringBuilder();
            bool finished = false;
            do
            {
                char c = t.NextChar();
                switch (c)
                {
                    case (char)0:
                        Grumble("Unclosed string template");
                        return null;
                    case '`':
                        c = t.NextChar();
                        if (c == '`')
                        {
                            currentPart.Append('`');
                        }
                        else
                        {
                            Expression @fixed = new StringLiteral(currentPart.ToString());
                            components.Add(@fixed);
                            finished = true;
                            t.UnreadChar();
                            t.LookAhead();
                            NextToken();
                        }

                        break;
                    case '{':
                        c = t.NextChar();
                        if (c == '{')
                        {
                            currentPart.Append('{');
                        }
                        else
                        {
                            Expression @fixed = new StringLiteral(currentPart.ToString());
                            components.Add(@fixed);
                            currentPart.SetLength(0);
                            t.UnreadChar();
                            t.State = Tokenizer.DEFAULT_STATE;
                            t.LookAhead();
                            NextToken();
                            if (t.currentToken == Token.RCURLY)
                            {
                            }
                            else
                            {
                                Expression exp = ParseExpression();
                                RetainedStaticContext rscSJ = new RetainedStaticContext(env);
                                Expression fnSJ = SystemFunction.MakeCall("string-join", rscSJ, exp, new StringLiteral(StringValue.SINGLE_SPACE));
                                ExpressionTool.CopyLocationInfo(exp, fnSJ);
                                components.Add(fnSJ);
                                Expect(Token.RCURLY);
                            }
                        }

                        break;
                    case '}':
                        c = t.NextChar();
                        if (c == '}')
                        {
                            currentPart.Append('}');
                        }
                        else
                        {
                            Grumble("Closing brace ('}') in string template must be doubled");
                        }

                        break;
                    default:
                        currentPart.Append(c);
                        break;
                }
            }
            while (!finished);
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            Block block = new Block(components.ToArray(new Expression[] { }));
            SetLocation(block);
            Expression fn = SystemFunction.MakeCall("string-join", rsc, block, new StringLiteral(StringValue.EMPTY_STRING));
            ExpressionTool.CopyLocationInfo(block, fn);
            components.Add(fn);
            return fn;
        }

        public virtual StructuredQName ParseVariableName()
        {
            NextToken();
            Expect(Token.NAME);
            string var = t.currentTokenValue;
            NextToken();
            if (scanOnly)
            {
                return new StructuredQName("", NamespaceUri.SAXON_GENERATED_VARIABLE, "dummy");
            }

            StructuredQName vtest = MakeStructuredQName(var, NamespaceUri.NULL);
            return vtest;
        }

        public virtual Expression ResolveVariableReference(int offset, StructuredQName vtest)
        {

            // See if it's a range variable or a variable in the context
            if (scanOnly)
            {
                return Literal.MakeEmptySequence();
            }

            ILocalBinding b = FindRangeVariable(vtest);
            Expression @ref;
            if (b != null)
            {
                @ref = new LocalVariableReference(b);
            }
            else
            {
                if (catchDepth > 0)
                {
                    foreach (StructuredQName errorVariable in StandardNames.errorVariables)
                    {
                        if (errorVariable.GetLocalPart().Equals(vtest.GetLocalPart()))
                        {
                            // Build saxon:dynamic-error-info(<name>) the same way the XSLT xsl:catch path does
                            // (ExpressionContext.BindVariable): MakeFunction + MakeFunctionCall. The BuiltInFunctionSet
                            // .Bind overload returned a node the parser then treated as a bound reference but which had
                            // no wired argument operand → SetDeepRetainedStaticContext walked a null child (NRE).
                            SystemFunction f = VendorFunctionSetHE.GetInstance().MakeFunction("dynamic-error-info", 1);
                            Expression call = f.MakeFunctionCall(new StringLiteral(vtest.GetLocalPart()));
                            SetLocation(call, offset);
                            return call;
                        }
                    }
                }

                try
                {
                    @ref = env.BindVariable(vtest);
                }
                catch (XPathException err)
                {
                    throw err.MaybeWithLocation(MakeLocation());
                }
            }

            SetLocation(@ref, offset);
            return @ref;
        }

        protected virtual Literal MakeStringLiteral(string currentTokenValue, bool unescape)
        {
            StringLiteral literal = new StringLiteral(currentTokenValue);
            SetLocation(literal);
            return literal;
        }

        protected virtual string Unescape(string token)
        {
            return token;
        }

        protected virtual Expression ParseConstructor()
        {
            Grumble("Node constructor expressions are allowed only in XQuery, not in XPath");
            return new ErrorExpression();
        }

        public virtual Expression ParseDynamicFunctionCall(Expression functionItem, Expression prefixArgument)
        {
            CheckLanguageVersion30();
            List<Expression> args = new List<Expression>(10);
            if (prefixArgument != null)
            {
                args.Add(prefixArgument);
            }

            IntSet placeMarkers = null;

            // the "(" has already been read by the Tokenizer: now parse the arguments
            NextToken();
            if (t.currentToken != Token.RPAR)
            {
                while (true)
                {
                    Expression arg;
                    int peek = t.PeekAhead();
                    if (t.currentToken == Token.QMARK && (peek == Token.COMMA || peek == Token.RPAR))
                    {
                        NextToken();

                        // this is a "?" placemarker
                        if (placeMarkers == null)
                        {
                            placeMarkers = new IntArraySet();
                        }

                        placeMarkers.Add(args.Count);
                        arg = Literal.MakeEmptySequence(); // a convenient fiction
                    }
                    else
                    {
                        arg = ParseFunctionArgument();
                    }

                    args.Add(arg);
                    if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else if (t.currentToken == Token.COLON)
                    {
                        Grumble("Keyword arguments are not allowed in a dynamic function call");
                    }
                    else
                    {
                        break;
                    }
                }

                Expect(Token.RPAR);
            }

            NextToken();
            if (placeMarkers == null)
            {
                return GenerateApplyCall(functionItem, args);
            }
            else
            {
                return CreateDynamicCurriedFunction(this, functionItem, args, placeMarkers);
            }
        }

        protected virtual Expression GenerateApplyCall(Expression functionItem, List<Expression> args)
        {
            DynamicFunctionCall call = new DynamicFunctionCall(functionItem, args);
            SetLocation(call, t.currentTokenStartOffset);
            return call;
        }

        protected virtual Expression ParseLookup(Expression lhs)
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            t.State = Tokenizer.BARE_NAME_STATE; // Prevent mis-recognition of x?f(2)
            t.currentToken = Token.LPAR; // Hack to force following symbol to be recognised in post-operator mode
            NextToken();
            int token = t.currentToken;
            t.State = Tokenizer.OPERATOR_STATE;
            Expression result;
            if (token == Token.NAME)
            {
                string name = t.currentTokenValue;
                if (!NameChecker.IsValidNCName(StringTool.CodePoints(name)))
                {
                    Grumble("The name following '?' must be a valid NCName");
                }

                NextToken();
                result = LookupName(lhs, name);
            }
            else if (token == Token.NUMBER)
            {
                NumericValue number = NumericValue.ParseNumber(t.currentTokenValue);
                if (!(number is IntegerValue))
                {
                    Grumble("Number following '?' must be an integer");
                }

                NextToken();
                result = new LookupExpression(lhs, Literal.MakeLiteral(number));
            }
            else if (token == Token.MULT || token == Token.STAR)
            {
                NextToken();
                result = LookupStar(lhs);
            }
            else if (token == Token.LPAR)
            {
                t.State = Tokenizer.DEFAULT_STATE;
                result = new LookupExpression(lhs, ParseParenthesizedExpression());
            }
            else if (token == Token.STRING_LITERAL)
            {
                CheckLanguageVersion40();
                result = LookupName(lhs, t.currentTokenValue);
                NextToken();
            }
            else if (token == Token.DOLLAR)
            {
                CheckLanguageVersion40();
                offset = t.currentTokenStartOffset;
                StructuredQName varName = ParseVariableName();
                result = new LookupExpression(lhs, ResolveVariableReference(offset, varName));
            }
            else
            {
                Grumble("Unexpected " + Token.tokens[token] + " after '?'");
                return null;
            }

            SetLocation(result, offset);
            return result;
        }

        private Expression LookupName(Expression lhs, string rhs)
        {
            return new LookupExpression(lhs, new StringLiteral(rhs));
        }

        private static Expression LookupStar(Expression lhs)
        {
            return new LookupAllExpression(lhs);
        }

        protected virtual NodeTest ParseNodeTest(short nodeType)
        {
            int tok = t.currentToken;
            string tokv = t.currentTokenValue;
            switch (tok)
            {
                case Token.LPAR:
                    CheckLanguageVersion40();
                    return ParseUnionNodeTest(nodeType);
                case Token.NAME:
                    NextToken();
                    return MakeNameTest(nodeType, tokv, nodeType == Types.Type.ELEMENT);
                case Token.PREFIX:
                    NextToken();
                    return MakeNamespaceTest(nodeType, tokv);
                case Token.SUFFIX:
                    NextToken();
                    tokv = t.currentTokenValue;
                    Expect(Token.NAME);
                    NextToken();
                    return MakeLocalNameTest(nodeType, tokv);
                case Token.STAR:
                    NextToken();
                    return NodeKindTest.MakeNodeKindTest(nodeType);
                case Token.KEYWORD_LBRA:
                    return ParseKindTest();
                default:
                    Grumble("Unrecognized node test");
                    throw new XPathException(""); // unreachable instruction
            }
        }

        protected virtual NodeTest ParseUnionNodeTest(short nodeType)
        {
            NextToken();
            NodeTest test = ParseNodeTest(nodeType);
            while (t.currentToken == Token.UNION && !t.currentTokenValue.Equals("union"))
            {
                NextToken();
                test = new CombinedNodeTest(test, Token.UNION, ParseNodeTest(nodeType));
            }

            Expect(Token.RPAR);
            NextToken();
            return test;
        }

        private NodeTest ParseKindTest()
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            string typeName = t.currentTokenValue;
            bool empty = false;
            NextToken();
            if (t.currentToken == Token.RPAR)
            {
                empty = true;
                NextToken();
            }

            switch (typeName)
            {
                case "item":
                    Grumble("item() is not allowed in a path expression");
                    return null;
                case "node":
                    if (empty)
                    {
                        return AnyNodeTest.GetInstance();
                    }
                    else
                    {
                        Grumble("Expected ')': no arguments are allowed in node()");
                        return null;
                    }

                case "text":
                    if (empty)
                    {
                        return NodeKindTest.TEXT;
                    }
                    else
                    {
                        Grumble("Expected ')': no arguments are allowed in text()");
                        return null;
                    }

                case "comment":
                    if (empty)
                    {
                        return NodeKindTest.COMMENT;
                    }
                    else
                    {
                        Grumble("Expected ')': no arguments are allowed in comment()");
                        return null;
                    }

                case "namespace-node":
                    if (empty)
                    {
                        if (!IsNamespaceTestAllowed())
                        {
                            Grumble("namespace-node() test is not allowed in XPath 2.0/XQuery 1.0");
                        }

                        return NodeKindTest.NAMESPACE;
                    }
                    else
                    {
                        if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.NAME)
                        {
                            string nsName = t.currentTokenValue;
                            NextToken();
                            Expect(Token.RPAR);
                            NextToken();
                            return new NameTest(Types.Type.NAMESPACE, NamespaceUri.NULL, nsName, pool);
                        }
                        else
                        {
                            Grumble("No arguments are allowed in namespace-node()");
                            return null;
                        }
                    }

                case "document-node":
                    if (empty)
                    {
                        return NodeKindTest.DOCUMENT;
                    }
                    else
                    {
                        if (!(t.currentTokenValue.Equals("element") || t.currentTokenValue.Equals("schema-element")))
                        {
                            Grumble("Argument to document-node() must be element(...) or schema-element(...)");
                        }

                        NodeTest inner = ParseKindTest();
                        Expect(Token.RPAR);
                        NextToken();
                        return new DocumentNodeTest(inner);
                    }

                case "processing-instruction":
                    int fp = -1;
                    if (empty)
                    {
                        return NodeKindTest.PROCESSING_INSTRUCTION;
                    }
                    else if (t.currentToken == Token.STRING_LITERAL)
                    {
                        string piName = Whitespace.Trim(Unescape(t.currentTokenValue));
                        if (!NameChecker.IsValidNCName(StringTool.CodePoints(piName)))
                        {

                            // Became an error as a result of XPath erratum XP.E7
                            Grumble("Processing instruction name must be a valid NCName", "XPTY0004");
                        }
                        else
                        {
                            fp = pool.AllocateFingerprint(NamespaceUri.NULL, piName);
                        }
                    }
                    else if (t.currentToken == Token.NAME)
                    {
                        try
                        {
                            string[] parts = NameChecker.GetQNameParts(t.currentTokenValue);
                            if ((parts[0].Length == 0))
                            {
                                fp = pool.AllocateFingerprint(NamespaceUri.NULL, parts[1]);
                            }
                            else
                            {
                                Grumble("Processing instruction name must not contain a colon");
                            }
                        }
                        catch (QNameException e)
                        {
                            Grumble("Invalid processing instruction name. " + e.GetMessage());
                        }
                    }
                    else
                    {
                        Grumble("Processing instruction name must be a QName or a string literal");
                    }

                    NextToken();
                    Expect(Token.RPAR);
                    NextToken();
                    return new NameTest(Types.Type.PROCESSING_INSTRUCTION, fp, pool);
                case "schema-attribute":
                    if (empty)
                    {
                        Grumble(typeName + "schema-attribute() requires a name to be supplied");
                        return null;
                    }
                    else
                    {
                        Expect(Token.NAME);
                        string name = t.currentTokenValue;
                        fp = MakeFingerprint(name, false);
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        if (!env.IsImportedSchema(pool.GetURI(fp)))
                        {
                            Grumble("No schema has been imported for namespace '" + pool.GetURI(fp) + '\'', "XPST0008");
                        }

                        ISchemaDeclaration decl = env.GetConfiguration().GetAttributeDeclaration(fp);
                        if (decl == null)
                        {
                            Grumble("There is no declaration for attribute @" + name + " in an imported schema", "XPST0008");
                            return null;
                        }
                        else
                        {
                            return decl.MakeSchemaNodeTest();
                        }
                    }

                case "schema-element":
                    if (empty)
                    {
                        Grumble(typeName + "schema-element() requires a name to be supplied");
                        return null;
                    }
                    else
                    {
                        Expect(Token.NAME);
                        string name = t.currentTokenValue;
                        fp = MakeFingerprint(name, true);
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        if (!env.IsImportedSchema(pool.GetURI(fp)))
                        {
                            Grumble("No schema has been imported for namespace '" + pool.GetURI(fp) + '\'', "XPST0008");
                        }

                        ISchemaDeclaration decl = env.GetConfiguration().GetElementDeclaration(fp);
                        if (decl == null)
                        {
                            Grumble("There is no declaration for element " + name + " in an imported schema", "XPST0008");
                            return null;
                        }
                        else
                        {
                            return decl.MakeSchemaNodeTest();
                        }
                    }

                case "attribute":
                case "element":
                    return ParseElementOrAttributeTest(typeName, empty);
                default:

                    // can't happen!
                    Grumble("Unknown node kind " + typeName);
                    return null;
            }
        }

        // Parse an element(...) or attribute(...) kind test (the name-and-type forms), already past the
        // opening keyword; empty says whether the parentheses were empty.
        private NodeTest ParseElementOrAttributeTest(string typeName, bool empty)
        {
            bool isElementTest = typeName.Equals("element");
            int nodeKind = isElementTest ? Types.Type.ELEMENT : Types.Type.ATTRIBUTE;
            NodeTest nodeTest;
            if (empty)
            {
                return isElementTest ? NodeKindTest.ELEMENT : NodeKindTest.ATTRIBUTE;
            }

            IList<NodeTest> tests = ParseNameTestUnion(nodeKind);
            if (tests.Count == 1)
            {
                nodeTest = tests[0];
                if (!allowXPath40Syntax && (nodeTest is NamespaceTest || nodeTest is LocalNameTest))
                {
                    Grumble("Wildcard syntax in item types requires 4.0 to be enabled");
                }
            }
            else
            {
                if (!allowXPath40Syntax)
                {
                    Grumble("NameTestUnion syntax requires 4.0 to be enabled");
                }

                nodeTest = NameTestUnion.WithTests(tests);
            }

            if (t.currentToken == Token.RPAR)
            {
                NextToken();
                return nodeTest;
            }
            else if (t.currentToken == Token.COMMA)
            {
                NextToken();
                NodeTest result;
                if (t.currentToken == Token.NAME)
                {
                    StructuredQName contentType = MakeStructuredQName(t.currentTokenValue, env.GetDefaultElementNamespace());
                    NamespaceUri uri = contentType.GetNamespaceUri();
                    if (!(uri.Equals(NamespaceUri.SCHEMA) || env.IsImportedSchema(uri)))
                    {
                        Grumble("No schema has been imported for namespace '" + uri + '\'', "XPST0008");
                    }

                    ISchemaType schemaType = env.GetConfiguration().GetSchemaType(contentType);
                    if (schemaType == null)
                    {
                        Grumble("Unknown type name " + contentType.EQName, "XPST0008");
                        return null;
                    }

                    if (nodeKind == Types.Type.ATTRIBUTE && schemaType.IsComplexType())
                    {
                        Warning("An attribute cannot have a complex type", DAXonErrorCode.SXWN9041);
                    }

                    NextToken();
                    bool nillable = false;
                    if (t.currentToken == Token.QMARK)
                    {
                        nillable = true;
                        if (nodeKind == Types.Type.ATTRIBUTE)
                        {
                            Grumble("attribute() tests must not be nillable");
                        }

                        NextToken();
                    }

                    if ((schemaType == AnyType.INSTANCE && nillable) || (nodeKind == Types.Type.ATTRIBUTE && schemaType == AnySimpleType.INSTANCE))
                    {
                        result = nodeTest;
                    }
                    else
                    {
                        NodeTest typeTest = new ContentTypeTest(nodeKind, schemaType, env.GetConfiguration(), nillable);
                        if (nodeTest is NodeKindTest)
                        {

                            // this represents element(*,T) or attribute(*,T)
                            result = typeTest;
                        }
                        else
                        {
                            result = new CombinedNodeTest(nodeTest, Token.INTERSECT, typeTest);
                        }
                    }
                }
                else
                {
                    Grumble("Unexpected " + Token.tokens[t.currentToken] + " after ',' in SequenceType");
                    return null;
                }

                Expect(Token.RPAR);
                NextToken();
                return result;
            }
            else
            {
                Grumble("Expected ')' or ',' in SequenceType");
            }

            return null;
        }

        public virtual IList<NodeTest> ParseNameTestUnion(int nodeKind)
        {
            IList<NodeTest> tests = new List<NodeTest>();
            bool matchesAll = false;
            while (true)
            {
                string tokv = t.currentTokenValue;
                switch (t.currentToken)
                {
                    case Token.NAME:
                        NextToken();
                        tests.Add(MakeNameTest(nodeKind, tokv, true));
                        break;
                    case Token.PREFIX:
                        NextToken();
                        tests.Add(MakeNamespaceTest(nodeKind, tokv));
                        break;
                    case Token.SUFFIX:
                        NextToken();
                        tokv = t.currentTokenValue;
                        if (t.currentToken == Token.NAME)
                        {
                        }
                        else
                        {
                            Grumble("Expected name after '*:'");
                        }

                        NextToken();
                        tests.Add(MakeLocalNameTest(nodeKind, tokv));
                        break;
                    case Token.STAR:
                    case Token.MULT:
                        NextToken();
                        matchesAll = true;
                        break;
                    default:
                        Grumble("Unrecognized name test at " + Token.tokens[t.currentToken]);
                        return null;
                }

                if (t.currentToken == Token.UNION && !t.currentTokenValue.Equals("union"))
                {

                    // must be "|" not "union"!
                    NextToken();
                }
                else
                {
                    break;
                }
            }

            if (matchesAll)
            {

                // If there's a "*" in the list, then anything else gets swallowed
                tests.Clear();
                tests.Add(NodeKindTest.MakeNodeKindTest(nodeKind));
            }

            return tests;
        }

        protected virtual bool IsNamespaceTestAllowed()
        {
            return allowXPath30Syntax;
        }

        protected virtual void CheckLanguageVersion30()
        {
            if (!allowXPath30Syntax)
            {
                Grumble("To use XPath 3.0 syntax, you must configure the XPath parser to handle it");
            }
        }

        protected virtual void CheckLanguageVersion31()
        {
            if (!allowXPath31Syntax)
            {
                Grumble("The XPath parser is not configured to allow use of XPath 3.1 syntax");
            }
        }

        protected virtual void CheckLanguageVersion40()
        {
            string lang = GetLanguage();
            if (!allowXPath40Syntax)
            {
                Grumble("The parser is not configured to allow use of " + lang + " 4.0 syntax");
            }
        }

        protected virtual void CheckMapExtensions()
        {
            if (!(allowXPath31Syntax || allowXPath30XSLTExtensions))
            {
                Grumble("The XPath parser is not configured to allow use of the map syntax from XSLT 3.0 or XPath 3.1");
            }
        }

        public virtual void CheckSyntaxExtensions(string construct)
        {
            if (!allowXPath40Syntax)
            {
                Grumble("Saxon XPath syntax extensions have not been enabled: " + construct + " is not allowed");
            }
        }

        protected virtual Expression ParseMapExpression()
        {
            CheckMapExtensions();

            // have read the "map {"
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            IList<Expression> entries = new List<Expression>();
            // Parallel record of the raw key/value expressions: if every key turns out to be a
            // distinct xs:string literal we compile a FixedKeyMapConstructor instead of the
            // map:merge(map:entry...) chain (see the default case below).
            IList<Expression> valueExprs = new List<Expression>();
            List<string> literalKeys = new List<string>();
            bool allStringLiteralKeys = true;
            var seenKeys = new HashSet<string>();
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                while (true)
                {
                    Expression key = ParseExprSingle();
                    if (t.currentToken == Token.ASSIGN)
                    {
                        Grumble("The ':=' notation is no longer accepted in map expressions: use ':' instead");
                    }

                    Expect(Token.COLON);
                    NextToken();
                    Expression value = ParseExprSingle();
                    valueExprs.Add(value);
                    if (allStringLiteralKeys
                        && key is Literal keyLit && keyLit.GroundedValue is StringValue keySv
                        && !keySv.IsUntypedAtomic() && seenKeys.Add(keySv.GetStringValue()))
                    {
                        literalKeys.Add(keySv.GetStringValue());
                    }
                    else
                    {
                        allStringLiteralKeys = false;
                    }

                    Expression entry;
                    if (key is Literal && ((Literal)key).GroundedValue is AtomicValue && value is Literal)
                    {
                        entry = Literal.MakeLiteral(new SingleEntryMap((AtomicValue)((Literal)key).GroundedValue, ((Literal)value).GroundedValue));
                    }
                    else
                    {
                        entry = MapFunctionSet.GetInstance(31).MakeFunction("entry", 2).MakeFunctionCall(key, value);
                    }

                    entries.Add(entry);
                    if (t.currentToken == Token.RCURLY)
                    {
                        break;
                    }
                    else
                    {
                        Expect(Token.COMMA);
                        NextToken();
                    }
                }
            }

            t.LookAhead(); //manual lookahead after an RCURLY
            NextToken();
            Expression result;
            switch (entries.Count)
            {
                case 0:
                    result = Literal.MakeLiteral(new HashTrieMap());
                    break;
                case 1:
                    result = entries[0];
                    break;
                default:
                    if (allStringLiteralKeys && literalKeys.Count == entries.Count)
                    {
                        // Every key a distinct xs:string literal: skip merge/entry, build the map
                        // directly through a shared key layout. Source key order is preserved.
                        result = new FixedKeyMapConstructor(literalKeys.ToArray(), valueExprs);
                        break;
                    }

                    Expression[] entriesArray = new Expression[entries.Count];
                    Block block = new Block(entries.ToArray(entriesArray));
                    HashTrieMap options = new HashTrieMap();
                    options.InitialPut(new StringValue("duplicates"), new StringValue("reject"));
                    options.InitialPut(new QNameValue("", NamespaceUri.SAXON, "duplicates-error-code"), new StringValue("XQDY0137"));
                    result = MapFunctionSet.GetInstance(31).MakeFunction("merge", 2).MakeFunctionCall(block, Literal.MakeLiteral(options));
                    break;
            }

            SetLocation(result, offset);
            return result;
        }

        protected virtual Expression ParseArraySquareConstructor()
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            IList<Expression> members = new List<Expression>();
            NextToken();
            if (t.currentToken == Token.RSQB)
            {
                NextToken();
                SquareArrayConstructor arrayBlock = new SquareArrayConstructor(members);
                SetLocation(arrayBlock, offset);
                return arrayBlock;
            }

            while (true)
            {
                Expression member = ParseExprSingle();
                members.Add(member);
                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                    continue;
                }
                else if (t.currentToken == Token.RSQB)
                {
                    NextToken();
                    break;
                }

                Grumble("Expected ',' or ']', " + "found " + Token.tokens[t.currentToken]);
                return new ErrorExpression();
            }

            SquareArrayConstructor block = new SquareArrayConstructor(members);
            block.SetLocation(MakeLocation(offset));
            return block;
        }

        protected virtual Expression ParseArrayCurlyConstructor()
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            NextToken();
            if (t.currentToken == Token.RCURLY)
            {
                t.LookAhead(); //manual lookahead after an RCURLY
                NextToken();
                return Literal.MakeLiteral(SimpleArrayItem.EMPTY_ARRAY);
            }

            Expression body = ParseExpression();
            Expect(Token.RCURLY);
            t.LookAhead(); //manual lookahead after an RCURLY
            NextToken();
            SystemFunction sf = ArrayFunctionSet.GetInstance(40).MakeFunction("_from-sequence", 1);
            Expression result = sf.MakeFunctionCall(body);
            SetLocation(result, offset);
            return result;
        }

        public virtual Expression ParseFunctionCall(Expression prefixArgument)
        {
            string fname = t.currentTokenValue;
            int offset = t.currentTokenStartOffset;
            List<Expression> args = new List<Expression>(10);
            if (prefixArgument != null)
            {
                args.Add(prefixArgument);
            }

            StructuredQName functionName = ResolveFunctionName(fname);
            IntSet placeMarkers = null;

            // the "(" has already been read by the Tokenizer: now parse the arguments
            Dictionary<StructuredQName, int> keywordArgs = null;
            NextToken();
            if (t.currentToken != Token.RPAR)
            {
                while (true)
                {
                    int peek = t.PeekAhead();
                    Expression arg;
                    if (t.currentToken == Token.NAME && peek == Token.ASSIGN && allowXPath40Syntax)
                    {

                        // keyword argument
                        StructuredQName paramName = qNameParser.Parse(t.currentTokenValue, NamespaceUri.NULL);
                        NextToken(); // read the operator
                        NextToken(); // position on the expression giving the value
                        arg = ParseExprSingle();
                        if (keywordArgs == null)
                        {
                            keywordArgs = new Dictionary<StructuredQName, int>();
                        }
                        else if (keywordArgs.ContainsKey(paramName))
                        {
                            Grumble("Duplicate keyword '" + paramName + "'in function arguments");
                        }

                        keywordArgs.Put(paramName, args.Count);
                        args.Add(arg);
                    }
                    else
                    {
                        if (keywordArgs != null)
                        {
                            Grumble("Keyword arguments must not be followed by positional arguments in a function call");
                        }

                        if (t.currentToken == Token.QMARK && (peek == Token.COMMA || peek == Token.RPAR))
                        {
                            NextToken();

                            // this is a "?" placemarker
                            if (placeMarkers == null)
                            {
                                placeMarkers = new IntArraySet();
                            }

                            placeMarkers.Add(args.Count);
                            arg = Literal.MakeEmptySequence(); // a convenient fiction
                        }
                        else
                        {
                            arg = ParseFunctionArgument();
                        }

                        args.Add(arg);
                    }

                    if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else
                    {
                        break;
                    }
                }

                Expect(Token.RPAR);
            }

            NextToken();
            if (scanOnly)
            {
                return new StringLiteral(StringValue.EMPTY_STRING);
            }

            Expression[] arguments = new Expression[args.Count];
            arguments = args.ToArray(arguments);
            if (placeMarkers != null)
            {
                return MakeCurriedFunction(this, offset, functionName, arguments, placeMarkers);
            }

            Expression fcall;
            SymbolicName.F sn = new SymbolicName.F(functionName, args.Count);
            IList<string> reasons = new List<string>();
            try
            {
                fcall = env.GetFunctionLibrary().Bind(sn, arguments, keywordArgs, env, reasons);
            }
            catch (UncheckedXPathException e)
            {
                fcall = null;
                reasons.Add(e.GetMessage());
            }

            if (fcall == null)
            {
                return ReportMissingFunction(offset, functionName, arguments, reasons);
            }


            //  A QName or NOTATION constructor function must be given the namespace context now
            //        if (fcall instanceof CastExpression &&
            //            ((CastExpression) fcall).bindStaticContext(env);
            //        }
            // There are special rules for certain functions appearing in a pattern
            if (language == ParsedLanguage.XSLT_PATTERN)
            {
                if (fcall.IsCallOn(typeof(RegexGroup)))
                {
                    return Literal.MakeEmptySequence();
                }
                else if (fcall is CurrentGroupCall)
                {
                    Grumble("The current-group() function cannot be used in a pattern", "XTSE1060", offset);
                    return new ErrorExpression();
                }
                else if (fcall is CurrentGroupingKeyCall)
                {
                    Grumble("The current-grouping-key() function cannot be used in a pattern", "XTSE1070", offset);
                    return new ErrorExpression();
                }
                else if (fcall.IsCallOn(typeof(CurrentMergeGroup)))
                {
                    Grumble("The current-merge-group() function cannot be used in a pattern", "XTSE3470", offset);
                    return new ErrorExpression();
                }
                else if (fcall.IsCallOn(typeof(CurrentMergeKey)))
                {
                    Grumble("The current-merge-key() function cannot be used in a pattern", "XTSE3500", offset);
                    return new ErrorExpression();
                }
            }

            SetLocation(fcall, offset);
            foreach (Expression argument in arguments)
            {
                if (fcall != argument && argument.ParentExpression == null && !functionName.HasURI(NamespaceUri.GLOBAL_JS))
                {

                    // avoid doing this when the function has already been optimized away, e.g. unordered()
                    // Also avoid doing this when a js: function is parsed into an ixsl:call()
                    // TODO move the adoptChildExpression into individual function libraries
                    fcall.AdoptChildExpression(argument);
                }
            }

            return MakeTracer(fcall, functionName);
        }

        public virtual Expression MakeCurriedFunction(XPathParser parser, int offset, StructuredQName name, Expression[] args, IntSet placeMarkers)
        {
            IStaticContext env = parser.GetStaticContext();
            IFunctionLibrary lib = env.GetFunctionLibrary();
            SymbolicName.F sn = new SymbolicName.F(name, args.Length);
            IFunctionItem target = lib.GetFunctionItem(sn, env);
            if (target == null)
            {

                // This will not happen in XQuery; instead, a dummy function will be created in the
                // UnboundFunctionLibrary in case it's a forward reference to a function not yet compiled
                IList<string> reasons = new List<string>();
                return parser.ReportMissingFunction(offset, name, args, reasons);
            }

            Expression targetExp = MakeNamedFunctionReference(name, target);
            parser.SetLocation(targetExp, offset);
            return CurryFunction(targetExp, args, placeMarkers);
        }

        public static Expression CurryFunction(Expression functionExp, Expression[] args, IntSet placeMarkers)
        {
            IIntIterator ii = placeMarkers.IIterator();
            while (ii.MoveNext())
            {
                args[ii.Current] = null;
            }

            return new PartialApply(functionExp, args);
        }

        public virtual Expression CreateDynamicCurriedFunction(XPathParser p, Expression functionItem, List<Expression> args, IntSet placeMarkers)
        {
            Expression[] arguments = new Expression[args.Count];
            arguments = args.ToArray(arguments);
            Expression result = CurryFunction(functionItem, arguments, placeMarkers);
            p.SetLocation(result, p.GetTokenizer().currentTokenStartOffset);
            return result;
        }

        public virtual void HandleExternalFunctionDeclaration(XQueryParser p, XQueryFunction func)
        {
            parserExtension.NeedExtension(p, "External function declarations");
        }

        private Expression MakeMapExpression(Dictionary<string, Expression> keywordArgs)
        {
            Expression[] block = new Expression[keywordArgs.Count];
            int i = 0;
            foreach (KeyValuePair<string, Expression> entry in keywordArgs.EntrySet())
            {
                StringLiteral key = new StringLiteral(entry.Key);
                block[i++] = MapFunctionSet.GetInstance(31).MakeFunction("entry", 2).MakeFunctionCall(key, entry.Value);
            }

            Block entries = new Block(block);
            return MapFunctionSet.GetInstance(31).MakeFunction("merge", 1).MakeFunctionCall(entries);
        }

        public virtual Expression ReportMissingFunction(int offset, StructuredQName functionName, Expression[] arguments, IList<string> reasons)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Cannot find a ").Append(arguments.Length).Append("-argument function named ").Append(functionName.EQName).Append("()");
            Configuration config = env.GetConfiguration();
            foreach (string reason in reasons)
            {
                sb.Append(". ").Append(reason);
            }

            if (config.GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                bool existsWithDifferentArity = false;
                for (int i = 0; i < arguments.Length + 5; i++)
                {
                    if (i != arguments.Length)
                    {
                        SymbolicName.F sn = new SymbolicName.F(functionName, i);
                        if (env.GetFunctionLibrary().IsAvailable(sn, 31))
                        {
                            existsWithDifferentArity = true;
                            break;
                        }
                    }
                }

                if (existsWithDifferentArity)
                {
                    sb.Append(". The namespace URI and local name are recognized, but the number of arguments is wrong");
                }
                else
                {
                    string supplementary = GetMissingFunctionExplanation(functionName, config);
                    if (supplementary != null)
                    {
                        sb.Append(". ").Append(supplementary);
                    }
                }
            }
            else
            {
                sb.Append(". External function calls have been disabled");
            }

            if (env.IsInBackwardsCompatibleMode())
            {

                // treat this as a dynamic error to be reported only if the function call is executed
                return new ErrorExpression(sb.ToString(), "XTDE1425", false);
            }
            else
            {
                Grumble(sb.ToString(), "XPST0017", offset);
                return null;
            }
        }

        public static string GetMissingFunctionExplanation(StructuredQName functionName, Configuration config)
        {
            string actualURI = functionName.GetNamespaceUri().ToString();
            string similarNamespace = NamespaceConstant.FindSimilarNamespace(actualURI);
            if (similarNamespace != null)
            {
                if (similarNamespace.Equals(actualURI))
                {
                    switch (similarNamespace)
                    {
                        case NamespaceConstant.FN:
                            return null;
                        case NamespaceConstant.SAXON:
                            if (config.EditionCode.Equals("HE"))
                            {
                                return "Saxon extension functions are not available under Saxon-HE";
                            }
                            else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
                            {
                                return "Saxon extension functions require a Saxon-PE or Saxon-EE license";
                            }

                            break;
                        case NamespaceConstant.XSLT:
                            if (functionName.GetLocalPart().Equals("original"))
                            {
                                return "Function name xsl:original is only available within an overriding function";
                            }
                            else
                            {
                                return "There are no functions defined in the XSLT namespace";
                            }
                    }
                }
                else
                {
                    return "Perhaps the intended namespace was '" + similarNamespace + "'";
                }
            }
            else if (actualURI.Contains("java"))
            {
                return DiagnoseCallToJavaMethod(config);
            }
            else if (actualURI.StartsWith("clitype:", StringComparison.Ordinal))
            {
                return DiagnoseCallToCliMethod(config);
            }

            return null;
        }

        private static string DiagnoseCallToJavaMethod(Configuration config)
        {
            if (config.EditionCode.Equals("HE"))
            {
                return "Reflexive calls to Java methods are not available under Saxon-HE";
            }
            else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
            {
                return "Reflexive calls to Java methods require a Saxon-PE or Saxon-EE license, and none was found";
            }
            else
            {
                return "For diagnostics on calls to Java methods, use the -TJ command line option " + "or set the Configuration property FeatureKeys.TRACE_EXTERNAL_FUNCTIONS";
            }
        }

        private static string DiagnoseCallToCliMethod(Configuration config)
        {
            if (config.EditionCode.Equals("HE"))
            {
                return "Reflexive calls to external .NET methods are not available under Saxon-HE";
            }
            else if (!config.IsLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION))
            {
                return "Reflexive calls to external .NET methods require a Saxon-PE or Saxon-EE license, and none was found";
            }
            else
            {
                return "For diagnostics on calls to .NET methods, use the -TJ command line option " + "or call processor.SetProperty(\"http://saxon.sf.net/feature/trace-external-functions\", \"true\")";
            }
        }

        protected virtual StructuredQName ResolveFunctionName(string fname)
        {
            if (scanOnly)
            {
                return NamespaceUri.SAXON.QName("dummy");
            }

            StructuredQName functionName = null;
            try
            {
                functionName = qNameParser.Parse(fname, env.GetDefaultFunctionNamespace());
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), e.ErrorCodeQName);
            }

            if (functionName.HasURI(NamespaceUri.SCHEMA))
            {
                Types.ItemType t = Types.Type.GetBuiltInItemType(functionName.GetNamespaceUri(), functionName.GetLocalPart());
                if (t is BuiltInAtomicType)
                {
                    CheckAllowedType(env, (BuiltInAtomicType)t);
                }
            }

            return functionName;
        }

        public virtual Expression ParseFunctionArgument()
        {
            return ParseExprSingle();
        }

        protected virtual Expression ParseNamedFunctionReference()
        {
            string fname = t.currentTokenValue;
            int offset = t.currentTokenStartOffset;
            IStaticContext env = GetStaticContext();

            // the "#" has already been read by the Tokenizer: now parse the arity
            NextToken();
            Expect(Token.NUMBER);
            NumericValue number = NumericValue.ParseNumber(t.currentTokenValue);
            if (!(number is IntegerValue))
            {
                Grumble("Number following '#' must be an integer");
            }

            if (number.CompareTo(0) < 0 || number.CompareTo(int.MaxValue) > 0)
            {
                Grumble("Number following '#' is out of range", "FOAR0002");
            }

            int arity = (int)number.LongValue();
            NextToken();
            StructuredQName functionName = null;
            try
            {
                functionName = GetQNameParser().Parse(fname, env.GetDefaultFunctionNamespace());
                if (functionName.GetPrefix().Equals(""))
                {
                    if (XPathParser.IsReservedFunctionName(functionName.GetLocalPart(), languageVersion))
                    {
                        Grumble("The unprefixed function name '" + functionName.GetLocalPart() + "' is reserved in XPath 3.1");
                    }
                }
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), e.ErrorCodeQName);
            }

            IFunctionItem fcf = null;
            try
            {
                IFunctionLibrary lib = env.GetFunctionLibrary();
                SymbolicName.F sn = new SymbolicName.F(functionName, arity);
                fcf = lib.GetFunctionItem(sn, env);
                if (fcf == null)
                {
                    Grumble("Function " + functionName.EQName + "#" + arity + " not found", "XPST0017", offset);
                }
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), "XPST0017", offset);
            }


            // Special treatment of functions in the system function library that depend on dynamic context; turn these
            // into calls on function-lookup()
            if (functionName.HasURI(NamespaceUri.FN) && fcf is SystemFunction)
            {
                BuiltInFunctionSet.Entry details = ((SystemFunction)fcf).Details;
                if (fcf is ContextAccessorFunction || (details != null && (details.properties & (BuiltInFunctionSet.FOCUS | BuiltInFunctionSet.DEPENDS_ON_STATIC_CONTEXT)) != 0))
                {

                    // For a context-dependent function, return a call on function-lookup(), which saves the context
                    SystemFunction lookup = XPath31FunctionSet.GetInstance().MakeFunction("function-lookup", 2);
                    lookup.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    return lookup.MakeFunctionCall(Literal.MakeLiteral(new QNameValue(functionName, BuiltInAtomicType.QNAME)), Literal.MakeLiteral(Int64Value.MakeIntegerValue(arity)));
                }
            }

            Expression @ref = MakeNamedFunctionReference(functionName, fcf);
            SetLocation(@ref, offset);
            return @ref;
        }

        private static Expression MakeNamedFunctionReference(StructuredQName functionName, IFunctionItem fcf)
        {
            if (fcf is UserFunction && !functionName.HasURI(NamespaceUri.XSLT))
            {

                // This case is treated specially because a UserFunctionReference in XSLT can be redirected
                // at link time to an overriding function. However, this doesn't apply to xsl:original
                return new UserFunctionReference((UserFunction)fcf);
            }
            else if (fcf is UnresolvedXQueryFunctionItem)
            {
                return ((UnresolvedXQueryFunctionItem)fcf).FunctionReference;
            }
            else
            {
                return new FunctionLiteral(fcf);
            }
        }

        protected virtual AnnotationList ParseAnnotationsList()
        {
            Grumble("Function annotations are not allowed in XPath");
            return null;
        }

        protected virtual Expression ParseInlineFunction(AnnotationList annotations)
        {
            NextToken();
            IList<UserFunctionParameter> @params = new List<UserFunctionParameter>(8);
            Values.SequenceType resultType = Values.SequenceType.ANY_SEQUENCE;
            int paramSlot = 0;
            while (t.currentToken != Token.RPAR)
            {

                //     ParamList   ::=     Param ("," Param)*
                //     Param       ::=     "$" VarName  TypeDeclaration?
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string argName = t.currentTokenValue;
                StructuredQName argQName = MakeStructuredQName(argName, NamespaceUri.NULL);
                Values.SequenceType paramType = Values.SequenceType.ANY_SEQUENCE;
                NextToken();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    paramType = ParseSequenceType();
                }

                UserFunctionParameter arg = new UserFunctionParameter();
                arg.SetRequiredType(paramType);
                arg.SetVariableQName(argQName);
                arg.SetSlotNumber(paramSlot++);
                @params.Add(arg);
                if (t.currentToken == Token.RPAR)
                {
                    break;
                }
                else if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    Grumble("Expected ',' or ')' after function argument, found '" + Token.tokens[t.currentToken] + '\'');
                }
            }

            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            if (t.currentToken == Token.AS)
            {
                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                resultType = ParseSequenceType();
            }

            return ParseInlineFunctionBody(annotations, @params, resultType);
        }

        protected virtual Expression ParseInlineFunctionBody(AnnotationList annotations, IList<UserFunctionParameter> @params, Values.SequenceType resultType)
        {

            // the next token should be the "{" at the start of the function body
            int offset = t.currentTokenStartOffset;
            InlineFunctionDetails details = new InlineFunctionDetails();
            details.outerVariables = new IndexedStack<ILocalBinding>();
            foreach (ILocalBinding lb in RangeVariables)
            {
                details.outerVariables.IPush(lb);
            }

            details.outerVariablesUsed = new List<ILocalBinding>(4);
            details.implicitParams = new List<UserFunctionParameter>(4);
            inlineFunctionStack.IPush(details);

            RangeVariables = new IndexedStack<ILocalBinding>();
            HashSet<StructuredQName> paramNameSet = new HashSet<StructuredQName>(8);
            foreach (UserFunctionParameter arg in @params)
            {
                if (!scanOnly)
                {
                    if (!paramNameSet.Add(arg.GetVariableQName()))
                    {
                        Grumble("Duplicate parameter name " + Err.Wrap(arg.GetVariableQName().EQName, Err.VARIABLE), "XQST0039");
                    }
                }

                DeclareRangeVariable(arg);
            }

            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression body;
            if (t.currentToken == Token.RCURLY && IsAllowXPath31Syntax())
            {
                t.LookAhead();
                NextToken();
                body = Literal.MakeEmptySequence();
            }
            else
            {
                body = ParseExpression();
                Expect(Token.RCURLY);
                t.LookAhead(); // must be done manually after an RCURLY
                NextToken();
            }

            ExpressionTool.SetDeepRetainedStaticContext(body, GetStaticContext().MakeRetainedStaticContext());
            Expression result = MakeInlineFunctionValue(this, annotations, details, @params, resultType, body);
            SetLocation(result, offset);
            foreach (UserFunctionParameter arg in @params)
            {
                UndeclareRangeVariable();
            }


            // restore the previous stack of range variables
            RangeVariables = details.outerVariables;
            inlineFunctionStack.Pop();
            return result;
        }

        public static Expression MakeInlineFunctionValue(XPathParser p, AnnotationList annotations, InlineFunctionDetails details, IList<UserFunctionParameter> @params, Values.SequenceType resultType, Expression body)
        {

            // Does this function access any outer variables?
            // If so, we create a UserFunction in which the outer variables are defined as extra parameters
            // in addition to the declared parameters, and then we return a call to partial-apply() that
            // sets these additional parameters to the values they have in the calling context.
            int arity = @params.Count;
            UserFunction uf = new UserFunction();
            uf.SetFunctionName(new StructuredQName("anon", NamespaceUri.ANONYMOUS, "f_" + uf.GetHashCode()));
            uf.SetPackageData(p.GetStaticContext().GetPackageData());
            uf.SetBody(body);
            uf.SetAnnotations(annotations);
            uf.ResultType = resultType;
            uf.IncrementReferenceCount();
            if (uf.GetPackageData() is StylesheetPackage)
            {

                // Add the inline function as a private component to the package, so that it can have binding
                // slots allocated for any references to global variables or functions, and so that it will
                // be copied as a hidden component into any using packages
                StylesheetPackage pack = (StylesheetPackage)uf.GetPackageData();
                Component comp = Component.MakeComponent(uf, Visibility.PRIVATE, VisibilityProvenance.DEFAULTED, pack, pack);
                uf.DeclaringComponent = comp;
            }

            Expression result;
            IList<UserFunctionParameter> implicitParams = details.implicitParams;
            if (!implicitParams.IsEmpty())
            {
                int extraParams = implicitParams.Count;
                int expandedArity = @params.Count + extraParams;
                UserFunctionParameter[] paramArray = new UserFunctionParameter[expandedArity];
                for (int i = 0; i < @params.Count; i++)
                {
                    paramArray[i] = @params[i];
                }

                int k = @params.Count;
                foreach (UserFunctionParameter implicitParam in implicitParams)
                {
                    paramArray[k++] = implicitParam;
                }

                uf.SetParameterDefinitions(paramArray);
                SlotManager stackFrame = p.GetStaticContext().GetConfiguration().MakeSlotManager();
                for (int i = 0; i < expandedArity; i++)
                {
                    int slot = stackFrame.AllocateSlotNumber(paramArray[i].GetVariableQName(), paramArray[i]);
                    paramArray[i].SetSlotNumber(slot);
                }

                ExpressionTool.AllocateSlots(body, expandedArity, stackFrame);
                uf.SetStackFrameMap(stackFrame);
                result = new UserFunctionReference(uf);
                Expression[] partialArgs = new Expression[expandedArity];
                for (int i = 0; i < arity; i++)
                {
                    partialArgs[i] = null;
                }

                for (int ip = 0; ip < implicitParams.Count; ip++)
                {
                    UserFunctionParameter ufp = implicitParams[ip];
                    ILocalBinding binding = details.outerVariablesUsed[ip];
                    VariableReference var;
                    if (binding is ParserExtension.TemporaryXSLTVariableBinding)
                    {
                        var = new LocalVariableReference(binding);
                        ((ParserExtension.TemporaryXSLTVariableBinding)binding).declaration.RegisterReference(var);
                    }
                    else
                    {
                        var = new LocalVariableReference(binding);
                    }

                    var.SetStaticType(binding.GetRequiredType(), null, 0);
                    ufp.SetRequiredType(binding.GetRequiredType());
                    partialArgs[ip + arity] = var;
                }

                result = new PartialApply(result, partialArgs);
            }
            else
            {

                // there are no implicit parameters
                UserFunctionParameter[] paramArray = @params.ToArray(new UserFunctionParameter[0]);
                uf.SetParameterDefinitions(paramArray);
                SlotManager stackFrame = p.GetStaticContext().GetConfiguration().MakeSlotManager();
                foreach (UserFunctionParameter param in paramArray)
                {
                    stackFrame.AllocateSlotNumber(param.GetVariableQName(), param);
                }

                ExpressionTool.AllocateSlots(body, @params.Count, stackFrame);
                uf.SetStackFrameMap(stackFrame);
                result = new UserFunctionReference(uf);
            }

            if (uf.GetPackageData() is StylesheetPackage)
            {

                // Note: inline functions in XSLT are registered as components; but not if they
                // are declared within a static expression, e.g. the initializer of a static
                // global variable
                ((StylesheetPackage)uf.GetPackageData()).AddComponent(uf.DeclaringComponent);
            }

            return result;
        }

        public virtual ILocalBinding FindOuterRangeVariable(StructuredQName qName)
        {
            return FindOuterRangeVariable(qName, inlineFunctionStack, GetStaticContext());
        }

        public static ILocalBinding FindOuterRangeVariable(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, IStaticContext env)
        {

            // If we didn't find the variable, it might be defined in an outer scope.
            ILocalBinding b2 = FindOuterXPathRangeVariable(qName, inlineFunctionStack);
            if (b2 != null)
            {
                return b2;
            }


            // It's not an in-scope range variable. If this is a free-standing XPath expression, it might be
            // a parameter declared in the static context
            if (env is IndependentContext && !inlineFunctionStack.IsEmpty())
            {
                b2 = FindXPathParameter(qName, inlineFunctionStack, env);
            }


            // It's not an in-scope range variable. If we're in XSLT, it might be an XSLT-defined local variable
            if (env is ExpressionContext && !inlineFunctionStack.IsEmpty())
            {
                b2 = FindOuterXSLTVariable(qName, inlineFunctionStack, env);
            }

            return b2; // if null, it's not an in-scope range variable
        }

        private static ILocalBinding FindOuterXPathRangeVariable(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack)
        {
            for (int s = inlineFunctionStack.size() - 1; s >= 0; s--)
            {
                InlineFunctionDetails details = inlineFunctionStack[s];
                IndexedStack<ILocalBinding> outerVariables = details.outerVariables;
                for (int v = outerVariables.size() - 1; v >= 0; v--)
                {
                    ILocalBinding b2 = outerVariables[v];
                    if (b2.GetVariableQName().Equals(qName))
                    {
                        for (int bs = s; bs <= inlineFunctionStack.Count - 1; bs++)
                        {
                            details = inlineFunctionStack[bs];
                            bool found = false;
                            for (int p = 0; p < details.outerVariablesUsed.Count - 1; p++)
                            {
                                if (details.outerVariablesUsed[p] == b2)
                                {

                                    // the inner function already uses the outer variable
                                    b2 = details.implicitParams[p];
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {

                                // Need to add an implicit parameter to the inner function
                                details.outerVariablesUsed.Add(b2);
                                UserFunctionParameter ufp = new UserFunctionParameter();
                                ufp.SetVariableQName(qName);
                                ufp.SetRequiredType(b2.GetRequiredType());
                                details.implicitParams.Add(ufp);
                                b2 = ufp;
                            }
                        }

                        return b2;
                    }
                }

                ILocalBinding b3 = BindParametersInNestedFunctions(qName, inlineFunctionStack, s);
                if (b3 != null)
                {
                    return b3;
                }
            }

            return null;
        }

        private static ILocalBinding FindXPathParameter(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, IStaticContext env)
        {
            if (env is IndependentContext)
            {
                XPathVariable var = ((IndependentContext)env).GetExternalVariable(qName);
                if (var != null)
                {
                    InlineFunctionDetails details = inlineFunctionStack[0];
                    ILocalBinding innermostBinding;
                    bool found = false;
                    for (int p = 0; p < details.outerVariablesUsed.Count; p++)
                    {
                        if (details.outerVariablesUsed[p].GetVariableQName().Equals(qName))
                        {

                            // the inner function already uses the outer variable
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {

                        // Need to add an implicit parameter to the inner function
                        details.outerVariablesUsed.Add(var);
                        UserFunctionParameter ufp = new UserFunctionParameter();
                        ufp.SetVariableQName(qName);
                        ufp.SetRequiredType(var.GetRequiredType());
                        details.implicitParams.Add(ufp);
                    }


                    // Now do the same for all inner inline functions, but this time binding to the
                    // relevant parameter of the next containing function
                    innermostBinding = BindParametersInNestedFunctions(qName, inlineFunctionStack, 0);
                    return innermostBinding;
                }
            }

            return null;
        }

        private static ILocalBinding FindOuterXSLTVariable(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, IStaticContext env)
        {
            StructuredQName attName = ((ExpressionContext)env).AttributeName;
            SourceBinding decl = ((ExpressionContext)env).GetStyleElement().BindLocalVariable(qName, attName);
            if (decl != null)
            {
                InlineFunctionDetails details = inlineFunctionStack[0];
                ILocalBinding innermostBinding;
                bool found = false;
                for (int p = 0; p < details.outerVariablesUsed.Count; p++)
                {
                    if (details.outerVariablesUsed[p].GetVariableQName().Equals(qName))
                    {

                        // the inner function already uses the outer variable
                        found = true;
                        break;
                    }
                }

                if (!found)
                {

                    // Need to add an implicit parameter to the inner function
                    details.outerVariablesUsed.Add(new ParserExtension.TemporaryXSLTVariableBinding(decl));
                    UserFunctionParameter ufp = new UserFunctionParameter();
                    ufp.SetVariableQName(qName);
                    ufp.SetRequiredType(decl.GetInferredType(true));
                    details.implicitParams.Add(ufp);
                }


                // Now do the same for all inner inline functions, but this time binding to the
                // relevant parameter of the next containing function
                innermostBinding = BindParametersInNestedFunctions(qName, inlineFunctionStack, 0);
                return innermostBinding;
            }

            return null;
        }

        private static ILocalBinding BindParametersInNestedFunctions(StructuredQName qName, IndexedStack<InlineFunctionDetails> inlineFunctionStack, int start)
        {
            InlineFunctionDetails details = inlineFunctionStack[start];
            IList<UserFunctionParameter> @params = details.implicitParams;
            foreach (UserFunctionParameter param in @params)
            {
                if (param.GetVariableQName().Equals(qName))
                {

                    // The variable reference corresponds to a parameter of an outer inline function
                    // We potentially need to add implicit parameters to any inner inline functions, and
                    // bind the variable reference to the innermost of these implicit parameters
                    ILocalBinding b2 = param;
                    for (int bs = start + 1; bs <= inlineFunctionStack.Count - 1; bs++)
                    {
                        details = inlineFunctionStack[bs];
                        bool found = false;
                        for (int p = 0; p < details.outerVariablesUsed.Count - 1; p++)
                        {
                            if (details.outerVariablesUsed[p] == param)
                            {

                                // the inner function already uses the outer variable
                                b2 = details.implicitParams[p];
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {

                            // Need to add an implicit parameter to the inner function
                            details.outerVariablesUsed.Add(param);
                            UserFunctionParameter ufp = new UserFunctionParameter();
                            ufp.SetVariableQName(qName);
                            ufp.SetRequiredType(param.GetRequiredType());
                            details.implicitParams.Add(ufp);
                            b2 = ufp;
                        }
                    }

                    if (b2 != null)
                    {
                        return b2;
                    }
                }
            }

            return null;
        }

        public virtual Expression ParseFocusFunction(AnnotationList annotations)
        {
            CheckLanguageVersion40();

            //Tokenizer t = getTokenizer();
            int offset = t.currentTokenStartOffset;
            InlineFunctionDetails details = new InlineFunctionDetails();
            details.outerVariables = new IndexedStack<ILocalBinding>();
            foreach (ILocalBinding lb in RangeVariables)
            {
                details.outerVariables.IPush(lb);
            }

            details.outerVariablesUsed = new List<ILocalBinding>(4);
            details.implicitParams = new List<UserFunctionParameter>(4);
            inlineFunctionStack.IPush(details);
            RangeVariables = new IndexedStack<ILocalBinding>();
            NextToken();
            IList<UserFunctionParameter> @params = new List<UserFunctionParameter>(1);
            Values.SequenceType resultType = Values.SequenceType.ANY_SEQUENCE;
            StructuredQName argQName = new StructuredQName("saxon", NamespaceUri.SAXON, "dot");
            UserFunctionParameter arg = new UserFunctionParameter();
            arg.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
            arg.SetVariableQName(argQName);
            arg.SetSlotNumber(0);
            @params.Add(arg);
            Expression body;
            if (t.currentToken == Token.RCURLY)
            {
                t.LookAhead(); // must be done manually after an RCURLY
                NextToken();
                body = Literal.MakeEmptySequence();
            }
            else
            {
                body = ParseExpression();
                Expect(Token.RCURLY);
                t.LookAhead(); // must be done manually after an RCURLY
                NextToken();
                body.SetRetainedStaticContext(GetStaticContext().MakeRetainedStaticContext());
                LocalVariableReference @ref = new LocalVariableReference(arg);
                body = new ForEach(@ref, body);
            }

            Expression result = MakeInlineFunctionValue(this, AnnotationList.EMPTY, details, @params, resultType, body);
            SetLocation(result, offset);

            // restore the previous stack of range variables
            RangeVariables = details.outerVariables;
            inlineFunctionStack.Pop();
            return result;
        }
        public static bool IsReservedFunctionName(string name, int version)
        {
            int x = Array.BinarySearch(version >= 40 ? reservedFunctionNames40 : reservedFunctionNames31, name);
            return x >= 0;
        }

        public virtual void DeclareRangeVariable(ILocalBinding declaration)
        {
            rangeVariables.IPush(declaration);
        }

        /// <summary>
        /// Note when the most recently declared range variable has gone out of scope
        /// </summary>
        public virtual void UndeclareRangeVariable()
        {
            rangeVariables.Pop();
        }

        protected virtual ILocalBinding FindRangeVariable(StructuredQName qName)
        {
            for (int v = rangeVariables.size() - 1; v >= 0; v--)
            {
                ILocalBinding b = rangeVariables[v];
                if (b.GetVariableQName().Equals(qName))
                {
                    return b;
                }
            }

            return FindOuterRangeVariable(qName);
        }

        public virtual void SetRangeVariableStack(IndexedStack<ILocalBinding> stack)
        {
            rangeVariables = stack;
        }

        public int MakeFingerprint(string qname, bool useDefault)
        {
            if (scanOnly)
            {
                return StandardNames.XML_SPACE;
            }

            try
            {
                NamespaceUri defaultNS = useDefault ? env.GetDefaultElementNamespace() : NamespaceUri.NULL;
                StructuredQName sq = qNameParser.Parse(qname, defaultNS);
                return env.GetConfiguration().GetNamePool().AllocateFingerprint(sq.GetNamespaceUri(), sq.GetLocalPart());
            }
            catch (XPathException e)
            {
                Grumble(e.GetMessage(), e.ErrorCodeQName);
                return -1;
            }
        }

        public StructuredQName MakeStructuredQNameSilently(string qname, NamespaceUri defaultUri)
        {
            if (scanOnly)
            {
                return NamespaceUri.SAXON.QName("dummy");
            }

            return qNameParser.Parse(qname, defaultUri);
        }

        public StructuredQName MakeStructuredQName(string qname, NamespaceUri defaultUri)
        {
            try
            {
                return MakeStructuredQNameSilently(qname, defaultUri);
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage(), err.ErrorCodeQName);
                return NamespaceUri.NULL.QName("error"); // Not executed; here to keep the compiler happy
            }
        }

        public INodeName MakeNodeName(string qname, bool useDefault)
        {
            StructuredQName sq = MakeStructuredQNameSilently(qname, useDefault ? env.GetDefaultElementNamespace() : NamespaceUri.NULL);
            string prefix = sq.GetPrefix();
            NamespaceUri uri = sq.GetNamespaceUri();
            string local = sq.GetLocalPart();
            if (uri.IsEmpty())
            {
                int fp = env.GetConfiguration().GetNamePool().AllocateFingerprint(NamespaceUri.NULL, local);
                return new NoNamespaceName(local, fp);
            }
            else
            {
                int fp = env.GetConfiguration().GetNamePool().AllocateFingerprint(uri, local);
                return new FingerprintedQName(prefix, uri, local, fp);
            }
        }

        public virtual NodeTest MakeNameTest(int nodeKind, string qname, bool useDefault)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            NamespaceUri defaultNS = NamespaceUri.NULL;
            if (useDefault && nodeKind == Types.Type.ELEMENT && !qname.StartsWith("Q{", StringComparison.Ordinal) && !qname.Contains(":"))
            {
                UnprefixedElementMatchingPolicy policy = env.GetUnprefixedElementMatchingPolicy();
                switch (policy)
                {
                    case UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE:
                        defaultNS = env.GetDefaultElementNamespace();
                        break;
                    case UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE_OR_NONE:
                        defaultNS = env.GetDefaultElementNamespace();
                        StructuredQName q = MakeStructuredQName(qname, defaultNS);
                        int fp1 = pool.AllocateFingerprint(q.GetNamespaceUri(), q.GetLocalPart());
                        NameTest test1 = new NameTest(nodeKind, fp1, pool);
                        int fp2 = pool.AllocateFingerprint(NamespaceUri.NULL, q.GetLocalPart());
                        NameTest test2 = new NameTest(nodeKind, fp2, pool);
                        return new CombinedNodeTest(test1, Token.UNION, test2);
                    case UnprefixedElementMatchingPolicy.ANY_NAMESPACE:
                        if (!NameChecker.IsValidNCName(StringTool.CodePoints(qname)))
                        {
                            Grumble("Invalid name '" + qname + "'");
                        }

                        return new LocalNameTest(pool, nodeKind, qname);
                }
            }

            StructuredQName qName = MakeStructuredQName(qname, defaultNS);
            int fp = pool.AllocateFingerprint(qName.GetNamespaceUri(), qName.GetLocalPart());
            return new NameTest(nodeKind, fp, pool);
        }

        public virtual IQNameTest MakeQNameTest(int nodeKind, string qname)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            StructuredQName q = MakeStructuredQName(qname, NamespaceUri.NULL);
            int fp = pool.AllocateFingerprint(q.GetNamespaceUri(), q.GetLocalPart());
            return new NameTest(nodeKind, fp, pool);
        }

        public virtual NamespaceTest MakeNamespaceTest(int nodeKind, string prefix)
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            if (scanOnly)
            {

                // return an arbitrary namespace if we're only doing a syntax check
                return new NamespaceTest(pool, nodeKind, NamespaceUri.SAXON);
            }

            if (prefix.StartsWith("Q{", StringComparison.Ordinal))
            {
                string uri = prefix.Substring(2, prefix.Length - 4) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                return new NamespaceTest(pool, nodeKind, NamespaceUri.Of(uri));
            }

            try
            {
                StructuredQName sq = qNameParser.Parse(prefix + ":dummy", NamespaceUri.NULL);
                return new NamespaceTest(pool, nodeKind, sq.GetNamespaceUri());
            }
            catch (XPathException err)
            {
                Grumble(err.GetMessage(), err.ErrorCodeQName);
                return null;
            }
        }

        public virtual LocalNameTest MakeLocalNameTest(int nodeKind, string localName)
        {
            if (!NameChecker.IsValidNCName(StringTool.CodePoints(localName)))
            {
                Grumble("Local name [" + localName + "] contains invalid characters");
            }

            return new LocalNameTest(env.GetConfiguration().GetNamePool(), nodeKind, localName);
        }

        protected virtual void SetLocation(Expression exp)
        {
            SetLocation(exp, t.currentTokenStartOffset);
        }

        public virtual void SetLocation(Expression exp, int offset)
        {
            if (exp != null)
            {
                if (exp.GetLocation() == null || exp.GetLocation() == Loc.NONE)
                {
                    exp.SetLocation(MakeLocation(offset));
                }
            }
        }

        public virtual ILocation MakeLocation(int offset)
        {
            int line = t.GetLineNumber(offset);
            int column = t.GetColumnNumber(offset);
            return MakeNestedLocation(env.GetContainingLocation(), line, column, null);
        }

        public virtual void SetLocation(Clause clause, int offset)
        {
            int line = t.GetLineNumber(offset);
            int column = t.GetColumnNumber(offset);
            ILocation loc = MakeNestedLocation(env.GetContainingLocation(), line, column, null);
            clause.Location = loc;
            clause.SetPackageData(env.GetPackageData());
        }
        public virtual ILocation MakeLocation()
        {
            if (t.GetLineNumber() == mostRecentLocation.GetLineNumber() && t.GetColumnNumber() == mostRecentLocation.GetColumnNumber() && ((env.GetSystemId() == null && mostRecentLocation.GetSystemId() == null) || env.GetSystemId().Equals(mostRecentLocation.GetSystemId())))
            {
                return mostRecentLocation;
            }
            else
            {
                int line = t.GetLineNumber();
                int column = t.GetColumnNumber();
                mostRecentLocation = MakeNestedLocation(env.GetContainingLocation(), line, column, null);
                return mostRecentLocation;
            }
        }

        public virtual ILocation MakeNestedLocation(ILocation containingLoc, int line, int column, string nearbyText)
        {
            if (containingLoc is Loc && containingLoc.GetLineNumber() <= 1 && containingLoc.GetColumnNumber() == -1 && nearbyText == null)
            {

                // No extra information available about the container
                return new Loc(env.GetSystemId(), line + 1, column + 1);
            }
            else
            {
                return new NestedLocation(containingLoc, line, column, nearbyText);
            }
        }

        public virtual Expression MakeTracer(Expression exp, StructuredQName qName)
        {
            exp.SetRetainedStaticContextLocally(env.MakeRetainedStaticContext());
            return exp; //        if (codeInjector != null) {
            //            return codeInjector.inject(exp);
            //        } else {
            //            return exp;
            //        }
        }

        protected virtual bool IsKeyword(string s)
        {
            return t.currentToken == Token.NAME && t.currentTokenValue.Equals(s);
        }

        public virtual void SetScanOnly(bool scanOnly)
        {
            this.scanOnly = scanOnly;
        }

        public virtual void SetAllowAbsentExpression(bool allowEmpty)
        {
            this.allowAbsentExpression = allowEmpty;
        }

        public virtual bool IsAllowAbsentExpression()
        {
            return this.allowAbsentExpression;
        }
        public enum ParsedLanguage
        {
            XPATH,
            XSLT_PATTERN,
            SEQUENCE_TYPE,
            XQUERY,
            EXTENDED_ITEM_TYPE
        }

        public class InlineFunctionDetails
        {
            public IndexedStack<ILocalBinding> outerVariables; // Local variables defined in the immediate outer scope (the father scope)
            public IList<ILocalBinding> outerVariablesUsed; // Local variables from the outer scope that are actually used
            public IList<UserFunctionParameter> implicitParams; // Parameters corresponding (1:1) with the above
        }

        public interface IAccelerator
        {
            Expression Parse(Tokenizer t, IStaticContext env, string expression, int start, int terminator);
        }

        public class NestedLocation : ILocation
        {
            private readonly ILocation containingLocation;
            private readonly int localLineNumber;
            private readonly int localColumnNumber;
            private readonly string nearbyText;

            public virtual int LocalLineNumber => localLineNumber;

            public virtual string NearbyText => nearbyText;
            public NestedLocation(ILocation containingLocation, int localLineNumber, int localColumnNumber, string nearbyText)
            {
                this.containingLocation = containingLocation.SaveLocation();
                this.localLineNumber = localLineNumber;
                this.localColumnNumber = localColumnNumber;
                this.nearbyText = nearbyText;
            }

            public virtual ILocation GetContainingLocation()
            {
                return containingLocation;
            }

            public virtual int GetColumnNumber()
            {
                return localColumnNumber;
            }

            public virtual string GetSystemId()
            {
                return containingLocation.GetSystemId();
            }

            public virtual string GetPublicId()
            {
                return containingLocation.GetPublicId();
            }

            public virtual int GetLineNumber()
            {
                return containingLocation.GetLineNumber() + localLineNumber;
            }

            public virtual ILocation SaveLocation()
            {
                return this;
            }
        }
    }
}
