////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Patterns
{
    public abstract class Pattern : PseudoExpression
    {
        private double priority = 0.5;
        private bool recoverable = true;
        private string originalText;

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override int Dependencies => 0;
        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual int Fingerprint => -1;

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual double DefaultPriority => priority;

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual string OriginalText
        {
            get => originalText; set
            {
                originalText = value;
            }
        }
        public static Pattern Make(string pattern, IStaticContext env, PackageData packageData)
        {
            PatternParser parser = (PatternParser)env.GetConfiguration().NewExpressionParser("PATTERN", false, env);
            Pattern pat = parser.ParsePattern(pattern, env);
            pat.SetRetainedStaticContext(env.MakeRetainedStaticContext());

            pat = (Pattern)pat.Simplify();
            return pat;
        }

        protected static void ReplaceCurrent(Expression exp, ILocalBinding binding)
        {
            foreach (Operand o in exp.Operands())
            {
                Expression child = o.GetChildExpression();
                if (child.IsCallOn(typeof(Current)))
                {
                    LocalVariableReference @ref = new LocalVariableReference(binding);
                    o.SetChildExpression(@ref);
                }
                else
                {
                    ReplaceCurrent(child, binding);
                }
            }
        }

        public static bool PatternContainsVariable(Pattern pattern)
        {
            return pattern != null && (pattern.Dependencies & StaticProperty.DEPENDS_ON_LOCAL_VARIABLES) != 0;
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual void BindCurrent(ILocalBinding binding)
        {
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual bool MatchesCurrentGroup()
        {
            return false;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual bool IsRecoverable()
        {
            return recoverable;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual void SetRecoverable(bool recoverable)
        {
            this.recoverable = recoverable;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        protected virtual void HandleDynamicError(XPathException ex, IXPathContext context)
        {
            if (ex.HasErrorCode("XTDE0640"))
            {

                // Treat circularity error as fatal (test error213)
                throw ex;
            }

            if (!IsRecoverable())
            {

                // Typically happens when this is a pseudo-pattern used for scannable expressions when streaming
                throw ex;
            }

            context.GetController().Warning("An error occurred matching pattern {" + this + "}: " + ex.GetMessage(), ex.ErrorCodeQName.EQName, GetLocation());
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override Expression Simplify()
        {
            return this;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            return this;
        }

        // Bridge the Expression-typed virtuals to the Pattern-typed methods above (net472 covariant-return
        // workaround, hooks declared in PseudoExpression) — a generic Expression-tree walk must land HERE,
        // not in Expression's default child walk, or pattern-specific context handling is lost (number-0202).
        protected override Expression TypeCheckCovariant(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return TypeCheck(visitor, contextInfo);
        }

        protected override Expression OptimizeCovariant(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return Optimize(visitor, contextInfo);
        }

        protected override Expression SimplifyCovariant()
        {
            return Simplify();
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            return nextFree;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual bool IsMotionless()
        {

            // default implementation for subclasses
            return true;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return MatchesItem(context.GetContextItem(), context);
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public bool MatchesItem(IItem item, IXPathContext context)
        {
            try
            {
                return Matches(item, context);
            }
            catch (XPathException.Circularity e)
            {
                throw e;
            }
            catch (XPathException.StackOverflow e)
            {
                throw e;
            }
            catch (UncheckedXPathException ex)
            {
                if (System.Environment.GetEnvironmentVariable("SAXON_DBG_PAT") != null)
                    System.Console.WriteLine("[pat-err] " + ex.GetXPathException().Message);
                HandleDynamicError(ex.GetXPathException(), context);
                return false;
            }
            catch (XPathException ex)
            {
                if (System.Environment.GetEnvironmentVariable("SAXON_DBG_PAT") != null)
                    System.Console.WriteLine("[pat-err] " + ex.Message);
                HandleDynamicError(ex, context);
                return false;
            }
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public abstract bool Matches(IItem item, IXPathContext context);
        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {

            // default implementation ignores the anchor node
            return Matches(node, context);
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual ISequenceIterator SelectNodes(ITreeInfo document, IXPathContext context)
        {
            NodeInfo doc = document.GetRootNode();
            UType uType = GetUType();
            if (UType.DOCUMENT.Subsumes(uType))
            {
                if (MatchesItem(doc, context))
                {
                    return SingletonIterator.MakeIterator(doc);
                }
                else
                {
                    return EmptyIterator.OfNodes();
                }
            }
            else if (UType.ATTRIBUTE.Subsumes(uType))
            {
                IAxisIterator allElements = doc.IterateAxis(AxisInfo.DESCENDANT, NodeKindTest.ELEMENT);
                ISequenceIterator allAttributes = Expressions.MappingIterator.IMap(allElements, (item) => ((NodeInfo)item).IterateAxis(AxisInfo.ATTRIBUTE));
                return ItemMappingIterator.Filter(allAttributes, (item) => MatchesItem(item, context));
            }
            else if (UType.NAMESPACE.Subsumes(uType))
            {
                IAxisIterator allElements = doc.IterateAxis(AxisInfo.DESCENDANT, NodeKindTest.ELEMENT);
                ISequenceIterator allNamespaces = Expressions.MappingIterator.IMap(allElements, (item) => ((NodeInfo)item).IterateAxis(AxisInfo.NAMESPACE));
                return ItemMappingIterator.Filter(allNamespaces, (item) => MatchesItem(item, context));
            }
            else if (UType.CHILD_NODE_KINDS.Subsumes(uType))
            {
                NodeTest nodeTest;
                if (uType.Equals(UType.ELEMENT))
                {
                    nodeTest = NodeKindTest.ELEMENT; // common case, enables use of getAllElements()
                }
                else
                {
                    nodeTest = new MultipleNodeKindTest(uType);
                }

                IAxisIterator allChildren = doc.IterateAxis(AxisInfo.DESCENDANT, nodeTest);
                return ItemMappingIterator.Filter(allChildren, (item) => MatchesItem(item, context));
            }
            else
            {
                int axis = uType.Subsumes(UType.DOCUMENT) ? AxisInfo.DESCENDANT_OR_SELF : AxisInfo.DESCENDANT;
                IAxisIterator allChildren = doc.IterateAxis(axis);
                ISequenceIterator attributesOrSelf = Expressions.MappingIterator.IMap(allChildren, (item) =>
                {
                    IAxisIterator mapper = SingleNodeIterator.MakeIterator((NodeInfo)item);
                    if (uType.Subsumes(UType.NAMESPACE))
                    {
                        mapper = (IAxisIterator)new ConcatenatingAxisIterator(mapper, ((NodeInfo)item).IterateAxis(AxisInfo.NAMESPACE));
                    }

                    if (uType.Subsumes(UType.ATTRIBUTE))
                    {
                        mapper = (IAxisIterator)new ConcatenatingAxisIterator(mapper, ((NodeInfo)item).IterateAxis(AxisInfo.ATTRIBUTE));
                    }

                    return mapper;
                });
                return ItemMappingIterator.Filter(attributesOrSelf, (item) => MatchesItem(item, context));
            }
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public abstract UType GetUType();

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public abstract override Types.ItemType GetItemType();
        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual void SetPriority(double priority)
        {
            this.priority = priority;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override string ToString()
        {
            if (originalText != null)
            {
                return originalText;
            }
            else
            {
                return Reconstruct();
            }
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual string Reconstruct()
        {
            return "pattern matching " + GetItemType();
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual HostLanguage GetHostLanguage()
        {
            return HostLanguage.XSLT;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public virtual Pattern ConvertToTypedPattern(string val)
        {
            return null;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override Pattern ToPattern(Configuration config)
        {
            return this;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public abstract override void Export(ExpressionPresenter presenter);
        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public abstract override Expression Copy(RebindingMap rebindings);
        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return this;
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override string ToShortString()
        {
            return ToString();
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        public override Elaborator GetElaborator()
        {
            return new PatternElaborator();
        }

        /// <summary>
        /// Replace any calls on current() by a variable reference bound to the supplied binding
        /// </summary>
        private class PatternElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                Pattern pat = (Pattern)GetExpression();
                return (context) => pat.MatchesItem(context.GetContextItem(), context);
            }
        }
    }
}