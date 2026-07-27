////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization
{
    public class UnicodeNormalizer : ProxyReceiver
    {
        private readonly Normalizer.Form normForm;

        public virtual Normalizer.Form NormalizationForm => normForm;
        public UnicodeNormalizer(string form, IReceiver next) : base(next)
        {
            switch (form)
            {
                case "NFC":
                    normForm = Normalizer.Form.NFC;
                    break;
                case "NFD":
                    normForm = Normalizer.Form.NFD;
                    break;
                case "NFKC":
                    normForm = Normalizer.Form.NFKC;
                    break;
                case "NFKD":
                    normForm = Normalizer.Form.NFKD;
                    break;
                default:
                    throw new XPathException("Unknown normalization form " + form, "SESU0011");
            }
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            IAttributeMap am2 = attributes.Apply((attInfo) =>
            {
                string newValue = Normalize(StringView.Of(attInfo.Value), ReceiverOption.Contains(attInfo.GetProperties(), ReceiverOption.USE_NULL_MARKERS)).ToString();
                return new AttributeInfo(attInfo.GetNodeName(), attInfo.GetType(), newValue, attInfo.GetLocation(), attInfo.GetProperties());
            });
            nextReceiver.StartElement(elemName, type, am2, namespaces, location, properties);
        }

        /// <summary>
        /// Output character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (Whitespace.IsAllWhite(chars))
            {
                nextReceiver.Characters(chars, locationId, properties);
            }
            else
            {
                nextReceiver.Characters(Normalize(chars, ReceiverOption.Contains(properties, ReceiverOption.USE_NULL_MARKERS)), locationId, properties);
            }
        }

        /// <summary>
        /// Output character data
        /// </summary>
        public virtual UnicodeString Normalize(UnicodeString @in, bool containsNullMarkers)
        {
            if (@in is WhitespaceString)
            {
                return @in;
            }

            UnicodeString t = @in.Tidy();
            if (containsNullMarkers)
            {
                StringBuilder @out = new StringBuilder(t.Length32());
                string s = @in.ToString();
                int start = 0;
                int nextNull = s.IndexOf((char)0);
                while (nextNull >= 0)
                {
                    @out.Append(Normalizer.Normalize(s.Substring(start, nextNull - start) /*Java substring(begin,END) -> C# (start,LENGTH)*/, normForm));
                    @out.Append((char)0);
                    start = nextNull + 1;
                    nextNull = s.IndexOf((char)0, start);
                    @out.Append(s.Substring(start, nextNull - start) /*Java substring(begin,END) -> C# (start,LENGTH)*/);
                    @out.Append((char)0);
                    start = nextNull + 1;
                    nextNull = s.IndexOf((char)0, start);
                }

                @out.Append(Normalizer.Normalize(s.Substring(start), normForm));
                return StringView.Tidy(@out.ToString());
            }
            else
            {
                return StringView.Tidy(Normalizer.Normalize(@in.ToString(), normForm));
            }
        }
    }
}