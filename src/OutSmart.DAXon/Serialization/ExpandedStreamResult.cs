////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Charsets;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    public class ExpandedStreamResult
    {
        private readonly Configuration config;
        private Properties outputProperties;
        private readonly string systemId;
        private TextWriter writer;
        private System.IO.Stream outputStream;
        private ICharacterSet characterSet;
        private string encoding;
        private bool mustCloseAfterUse = false;

        public virtual TextWriter Writer
        {
            get => writer; set
            {
                this.writer = value;

                // If the writer uses a known encoding, change the encoding in the XML declaration
                // to match. Any encoding actually specified in xsl:output is ignored, because encoding
                // is being done by the user-supplied global::System.IO.TextWriter, and not by Saxon itself.
                if (value is StreamWriter && outputProperties != null)
                {
                    string enc = ((StreamWriter)value).Encoding.WebName;
                    outputProperties.SetProperty(OutputKeys.ENCODING, enc);
                    characterSet = config.GetCharacterSetFactory().GetCharacterSet(outputProperties);
                }
            }
        }

        public virtual ICharacterSet CharacterSet => characterSet;
        public ExpandedStreamResult(Configuration config, StreamResult result, Properties outputProperties)
        {
            this.config = config;
            this.systemId = result.GetSystemId();
            this.writer = result.GetWriter();
            this.outputStream = result.GetOutputStream();
            this.outputProperties = outputProperties;
            this.encoding = outputProperties.GetProperty(OutputKeys.ENCODING);
            if (encoding == null)
            {
                encoding = "UTF8";
            }
            else if (encoding.EqualsIgnoreCase("UTF-8"))
            {
                encoding = "UTF8";
            }
            else if (encoding.EqualsIgnoreCase("UTF-16"))
            {
                encoding = "UTF16";
            }

            if (characterSet == null)
            {
                characterSet = config.GetCharacterSetFactory().GetCharacterSet(encoding);
            }

            string byteOrderMark = outputProperties.GetProperty(DAXonOutputKeys.BYTE_ORDER_MARK);
            if ("no".Equals(byteOrderMark) && "UTF16".Equals(encoding))
            {

                // Java always writes a bom for UTF-16, so if the user doesn't want one, use utf16-be
                encoding = "UTF-16BE";
            }
            else if (!(characterSet is UTF8CharacterSet))
            {

                //if (characterSet instanceof PluggableCharacterSet) {
                encoding = characterSet.CanonicalName;
            }
        }

        public virtual IUnicodeWriter ObtainUnicodeWriter()
        {
            if (writer != null)
            {
                return new UnicodeWriterToWriter(writer);
            }
            else
            {
                System.IO.Stream os = ObtainOutputStream();
                return MakeUnicodeWriterFromOutputStream(os);
            }
        }

        protected virtual System.IO.Stream ObtainOutputStream()
        {
            if (outputStream != null)
            {
                return outputStream;
            }

            string uriString = systemId;
            if (uriString == null)
            {
                throw new XPathException("Result has no system ID, writer, or output stream defined");
            }

            try
            {
                string file = MakeWritableOutputFile(uriString);
                mustCloseAfterUse = true;
                outputStream = new FileStream(file, FileMode.Create, FileAccess.Write);
            }
            catch (FileNotFoundException fnf)
            {
                throw new XPathException(fnf);
            }
            catch (URISyntaxException fnf)
            {
                throw new XPathException(fnf);
            }
            catch (ArgumentException fnf)
            {
                throw new XPathException(fnf);
            }

            return outputStream;
        }

        public virtual bool IsMustCloseAfterUse()
        {
            return mustCloseAfterUse;
        }

        public static string MakeWritableOutputFile(string uriString)
        {
            URI uri = new URI(uriString);
            if (!uri.IsAbsolute())
            {
                try
                {
                    uri = new Uri(Path.GetFullPath(uriString)).AbsoluteUri;
                }
                catch (Exception e)
                {
                }
            }

            string file = new Uri(uri.ToString()).LocalPath;
            try
            {
                if ("file".Equals(uri.Scheme) && !(File.Exists(file) || Directory.Exists(file)))
                {
                    string directory = Path.GetDirectoryName(file);
                    if (directory != null && !(File.Exists(directory) || Directory.Exists(directory)))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    using (File.Create(file)) { }
                }

                if (Directory.Exists(file))
                {
                    throw new XPathException("Cannot write to a directory: " + uriString, DAXonErrorCode.SXRD0004);
                }

                if (File.Exists(file) && new FileInfo(file).IsReadOnly)
                {
                    throw new XPathException("Cannot write to URI " + uriString, DAXonErrorCode.SXRD0004);
                }
            }
            catch (IOException err)
            {
                throw new XPathException("Failed to create output file " + uri, err);
            }

            return file;
        }

        public virtual bool UsesWriter()
        {
            return true;
        }

        private TextWriter MakeWriterFromOutputStream(System.IO.Stream stream)
        {
            outputStream = stream;

            // If the user supplied an global::System.IO.Stream, but the Emitter is written to
            // use a global::System.IO.TextWriter (this is the most common case), then we create a global::System.IO.TextWriter
            // to wrap the supplied global::System.IO.Stream; the complications are to ensure that
            // the character encoding is correct.
            try
            {
                Charset javaEncoding;
                if (encoding.EqualsIgnoreCase("iso-646") || encoding.EqualsIgnoreCase("iso646"))
                {
                    javaEncoding = StandardCharsets.US_ASCII;
                }
                else
                {
                    javaEncoding = Charset.ForName(encoding);
                }

                if (encoding.EqualsIgnoreCase("UTF8"))
                {
                    writer = new UTF8Writer(outputStream);
                }
                else
                {
                    writer = new StreamWriter(outputStream, Encoding.GetEncoding(javaEncoding.ToString()));
                }

                return writer;
            }
            catch (Exception err)
            {
                if (encoding.EqualsIgnoreCase("UTF8"))
                {
                    throw new XPathException("Failed to create a UTF8 output writer");
                }

                throw new XPathException("Encoding " + encoding + " is not supported", "SESU0007");
            }
        }

        private IUnicodeWriter MakeUnicodeWriterFromOutputStream(System.IO.Stream stream)
        {
            outputStream = stream;
            try
            {
                if (encoding.EqualsIgnoreCase("UTF8"))
                {
                    return new UTF8Writer(outputStream);
                }
                else
                {
                    TextWriter writer = MakeWriterFromOutputStream(stream);
                    return new UnicodeWriterToWriter(writer);
                }
            }
            catch (Exception err)
            {
                if (encoding.EqualsIgnoreCase("UTF8"))
                {
                    throw new XPathException("Failed to create a UTF8 output writer");
                }

                throw new XPathException("Encoding " + encoding + " is not supported", "SESU0007");
            }
        }

        public virtual System.IO.Stream GetOutputStream()
        {
            return outputStream;
        }
    }
}