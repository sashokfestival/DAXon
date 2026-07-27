////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform.Stream;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Lib
{
    public class StandardLogger : Logger
    {
        private TextWriter writer = Console.Error;
        private int threshold = Logger.INFO;
        private bool mustClose = false;

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public virtual TextWriter PrintWriter
        {
            get => writer; set
            {
                this.writer = value;
            }
        }

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public virtual int Threshold
        {
            get => threshold; set
            {
                this.threshold = value;
            }
        }
        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public StandardLogger()
        {
        }

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        // IO-removal: StandardLogger(global::System.IO.TextWriter) dropped -- global::System.IO.TextWriter maps to System.IO.TextWriter, handled by StandardLogger(TextWriter).

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public StandardLogger(TextWriter writer)
        {
            PrintWriter = (TextWriter)writer;
        }

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public StandardLogger(string fileName)
        {
            PrintWriter = new StreamWriter(fileName) { AutoFlush = true };
            mustClose = true;
        }

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public virtual void SetPrintStream(TextWriter stream)
        {
            this.writer = stream;
        }

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public override StreamResult AsStreamResult()
        {
            return new StreamResult(writer);
        }

        /// <summary>
        /// Create a Logger that wraps the System.Console.Error output stream
        /// </summary>
        public override void Println(string message, int severity)
        {
            if (severity >= threshold)
            {
                writer.Write(message + "\n");
                writer.Flush();
            }
        }

        /// <summary>
        /// Close the logger, indicating that no further messages will be written
        /// </summary>
        public override void Dispose()
        {
            if (mustClose)
            {
                writer.Dispose();
            }
        }
    }
}
