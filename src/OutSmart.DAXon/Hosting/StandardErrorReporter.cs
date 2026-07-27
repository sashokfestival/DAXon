////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    public class StandardErrorReporter : StandardDiagnostics, IErrorReporter
    {
        private int warningCount = 0;
        private int maximumNumberOfWarnings = 25;
        private int errorCount = 0;
        private int maximumNumberOfErrors = 1000;
        private int maxOrdinaryCharacter = 255;
        private int stackTraceDetail = 2;
        private readonly HashSet<string> warningsIssued = new HashSet<string>();
        protected Logger logger = new StandardLogger();
        private IXmlProcessingError latestError;
        private bool outputErrorCodes = true;
        private HashSet<StructuredQName> suppressedWarnings;

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual Logger Logger
        {
            get => logger; set
            {
                this.logger = value;
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual int MaximumNumberOfWarnings
        {
            get => this.maximumNumberOfWarnings; set
            {
                this.maximumNumberOfWarnings = value;
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual int MaximumNumberOfErrors
        {
            get => this.maximumNumberOfErrors; set
            {
                this.maximumNumberOfErrors = value;
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual int MaxOrdinaryCharacter
        {
            get => maxOrdinaryCharacter; set
            {
                maxOrdinaryCharacter = value;
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual int StackTraceDetail
        {
            get => stackTraceDetail; set
            {
                stackTraceDetail = value;
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual int NumberOfWarnings => warningCount;

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual int NumberOfErrors => errorCount;

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual IXmlProcessingError LatestError => latestError;
        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public StandardErrorReporter()
        {
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual void SetOutputErrorCodes(bool include)
        {
            this.outputErrorCodes = include;
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual void SuppressWarning(string code)
        {
            if (suppressedWarnings == null)
            {
                suppressedWarnings = new HashSet<StructuredQName>();
            }

            if (code.StartsWith("Q{", StringComparison.Ordinal))
            {
                suppressedWarnings.Add(StructuredQName.FromEQName(code));
            }
            else
            {
                suppressedWarnings.Add(new StructuredQName("err", NamespaceConstant.ERR, code));
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual bool IsSuppressedWarning(StructuredQName errorCode)
        {
            return suppressedWarnings != null && suppressedWarnings.Contains(errorCode);
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public void Report(IXmlProcessingError processingError)
        {
            if (processingError != latestError)
            {
                latestError = processingError;
                if (processingError.IsWarning())
                {
                    if (processingError.GetErrorCode() == null || !IsSuppressedWarning(processingError.GetErrorCode().GetStructuredQName()))
                    {
                        Warning(processingError);
                    }
                }
                else
                {
                    Error(processingError);
                }
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        protected virtual void Warning(IXmlProcessingError error)
        {
            if (logger == null)
            {
                logger = new StandardLogger();
            }

            string message = ConstructMessage(error, "", "Warning ");
            if (!warningsIssued.Contains(message))
            {
                if (warningCount > MaximumNumberOfWarnings)
                {
                    if (warningCount == MaximumNumberOfWarnings + 1)
                    {
                        logger.Info("No more warnings will be displayed");
                    }
                }
                else
                {
                    logger.Warning(message);
                }

                warningCount++;
                warningsIssued.Add(message);
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual bool IsReportingWarnings()
        {
            return warningCount < MaximumNumberOfWarnings;
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        protected virtual void Error(IXmlProcessingError err)
        {
            if (errorCount++ > maximumNumberOfErrors)
            {
                err.TerminationMessage = "Too many errors reported";
            }

            if (logger == null)
            {
                logger = new StandardLogger();
            }

            string message;
            HostLanguage lang = err.GetHostLanguage();
            string langText = "";
            if (lang != HostLanguage.UNKNOWN)
            {
                switch (lang)
                {
                    case HostLanguage.XSLT:
                        break;
                    case HostLanguage.XQUERY:
                        langText = "in query ";
                        break;
                    case HostLanguage.XPATH:
                        langText = "in expression ";
                        break;
                    case HostLanguage.XML_SCHEMA:
                        langText = "in schema ";
                        break;
                    case HostLanguage.XSLT_PATTERN:
                        langText = "in pattern ";
                        break;
                }
            }

            string kind = "Error ";
            if (err.IsTypeError())
            {
                kind = "Type error ";
            }
            else if (err.IsStaticError())
            {
                kind = "Static error ";
            }

            message = ConstructMessage(err, langText, kind);
            logger.Error(message);
            if (err is XmlProcessingException)
            {
                XPathException exception = ((XmlProcessingException)err).GetXPathException();
                IXPathContext context = exception.XPathContext;
                if (context != null && !(context is EarlyEvaluationContext))
                {
                    OutputStackTrace(logger, context);
                }
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string ConstructMessage(IXmlProcessingError exception, string langText, string kind)
        {
            return ConstructFirstLine(exception, langText, kind) + "\n  " + ConstructSecondLine(exception);
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string ConstructFirstLine(IXmlProcessingError error, string langText, string kind)
        {
            ILocation locator = error.GetLocation();
            if (locator is AttributeLocation)
            {
                return kind + langText + GetLocationMessageText(locator);
            }
            else if (locator is XPathParser.NestedLocation)
            {
                XPathParser.NestedLocation nestedLoc = (XPathParser.NestedLocation)locator;
                ILocation outerLoc = nestedLoc.GetContainingLocation();
                int line = nestedLoc.LocalLineNumber + 1;
                int column = nestedLoc.GetColumnNumber() + 1;
                string lineInfo = line <= 1 ? "" : "on line " + line + ' ';
                string columnInfo = column <= 1 ? "" : "at " + (line <= 1 ? "char " : "column ") + column + ' ';
                string nearBy = nestedLoc.NearbyText;
                string extraContext = FormatExtraContext(error.GetFailingExpression(), nearBy);
                if (outerLoc is AttributeLocation)
                {

                    // Typical XSLT case
                    string innerLoc = lineInfo + extraContext + columnInfo;
                    return kind + innerLoc + langText + GetLocationMessageText(outerLoc);
                }
                else
                {

                    // Typical XQuery case; no extra information available from the outer location
                    string innerLoc = lineInfo + columnInfo;
                    if (outerLoc.GetLineNumber() > 1)
                    {
                        innerLoc += "(" + langText + "on line " + outerLoc.GetLineNumber() + ") ";
                    }

                    if (outerLoc.GetSystemId() != null)
                    {
                        innerLoc += "of " + outerLoc.GetSystemId() + " ";
                    }

                    return kind + extraContext + innerLoc;
                }
            }
            else
            {
                return kind + GetLocationMessage(error);
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string FormatExtraContext(Expression failingExpression, string nearBy)
        {
            if (failingExpression != null)
            {
                if (failingExpression.IsCallOn(typeof(Error)))
                {
                    return "signaled by call to error() ";
                }
                else
                {
                    return "evaluating (" + failingExpression.ToShortString() + ") ";
                }
            }
            else if (nearBy != null && !(nearBy.Length == 0))
            {
                return (nearBy.StartsWith("...", StringComparison.Ordinal) ? "near" : "in") + ' ' + Err.Wrap(nearBy) + " ";
            }
            else
            {
                return "";
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string ConstructSecondLine(IXmlProcessingError err)
        {
            return ExpandSpecialCharacters(WordWrap(GetExpandedMessage(err)));
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        protected virtual string GetLocationMessage(IXmlProcessingError err)
        {
            ILocation loc = err.GetLocation();
            return GetLocationMessageText(loc);
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string GetExpandedMessage(IXmlProcessingError err)
        {
            string message = FormatErrorCode(err) + " " + err.GetMessage();
            message = FormatNestedMessages(err, message);
            return message;
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string FormatNestedMessages(IXmlProcessingError err, string message)
        {
            if (err.GetCause() == null)
            {
                return message;
            }
            else
            {
                StringBuilder sb = new StringBuilder(message);
                Exception e = err.GetCause();
                while (e != null)
                {
                    if (e is Exception)
                    {
                        StringWriter sw = new StringWriter();
                        AppendStackTrace(e, sw);
                        sb.Append('\n').Append(sw);
                    }
                    else if (!message.Contains(e.GetMessage()))
                    {
                        sb.Append(". Caused by ").Append(e.GetType().GetName());
                    }

                    string next = e.GetMessage();
                    if (next != null)
                    {
                        sb.Append(": ").Append(next);
                    }

                    e = e.GetCause() as Exception ?? (e.GetCause() == null ? null : new Exception(e.GetCause().Message)); // message-only wrap: keeping the inner exception loops forever (wrap.GetCause()==cause)
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        private void AppendStackTrace(Exception e, StringWriter sw)
        {
            sw.WriteLine(e.ToString()); sw.WriteLine(e.StackTrace);
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string FormatErrorCode(IXmlProcessingError err)
        {
            if (outputErrorCodes)
            {
                QName qCode = err.GetErrorCode();
                if (qCode != null)
                {
                    if (qCode.GetNamespaceUri().Equals(NamespaceUri.ERR))
                    {
                        return qCode.LocalName + " ";
                    }
                    else
                    {
                        return qCode.ToString() + " ";
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        public virtual string ExpandSpecialCharacters(string @in)
        {
            if (logger.IsUnicodeAware())
            {
                return @in;
            }
            else
            {
                return ExpandSpecialCharacters(@in, maxOrdinaryCharacter);
            }
        }

        /// <summary>
        /// Create a Standard Error Reporter
        /// </summary>
        protected virtual void OutputStackTrace(Logger @out, IXPathContext context)
        {
            LogStackTrace(context, @out, stackTraceDetail);
        }
    }
}