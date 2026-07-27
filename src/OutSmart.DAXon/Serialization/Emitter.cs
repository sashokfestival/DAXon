////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    public abstract class Emitter : SequenceReceiver, IReceiverWithOutputProperties
    {
        protected IUnicodeWriter writer;
        protected Properties outputProperties;
        protected ICharacterSet characterSet;
        protected bool allCharactersEncodable = false;
        private bool mustClose = false;
        public Emitter() : base(null)
        {
        }

        public virtual void SetOutputProperties(Properties details)
        {
            if (characterSet == null)
            {
                characterSet = GetConfiguration().GetCharacterSetFactory().GetCharacterSet(details);
                allCharactersEncodable = (characterSet is UTF8CharacterSet || characterSet is UTF16CharacterSet);
            }

            outputProperties = details;
        }

        public Properties GetOutputProperties()
        {
            return outputProperties;
        }

        public virtual void SetUnicodeWriter(IUnicodeWriter unicodeWriter)
        {
            this.writer = unicodeWriter;
        }

        public virtual void SetMustClose(bool mustClose)
        {
            this.mustClose = mustClose;
        }

        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
        }

        /// <summary>
        /// Notify the end of the event stream
        /// </summary>
        public override void Dispose()
        {
            if (mustClose && writer != null)
            {
                try
                {
                    writer.Dispose();
                }
                catch (IOException e)
                {
                    throw new XPathException("Failed to close output stream");
                }
            }
        }

        /// <summary>
        /// Notify the end of the event stream
        /// </summary>
        public override bool UsesTypeAnnotations()
        {
            return false;
        }

        /// <summary>
        /// Append an arbitrary item (node or atomic value) to the output
        /// </summary>
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            if (item is NodeInfo)
            {
                Decompose(item, locationId, copyNamespaces);
            }
            else
            {
                Characters(item.UnicodeStringValue, locationId, ReceiverOption.NONE);
            }
        }
    }
}
