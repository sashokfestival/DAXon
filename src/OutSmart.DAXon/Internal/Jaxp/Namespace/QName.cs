////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// JAXP / javax.xml.transform stubs. Minimal interface/class shapes so transpiled Saxon code
// type-resolves. NOT functional — Saxon's TrAX/SAX bridging will be reworked in Phase 3.2 to
// use System.Xml.* natively.

using System;

namespace OutSmart.DAXon.Internal.Jaxp.Namespace
{
    public class QName
    {
        public string LocalPart { get; }
        public string NamespaceURI { get; }
        public string Prefix { get; }
        public QName(string localPart) { LocalPart = localPart; NamespaceURI = ""; Prefix = ""; }
        public QName(string namespaceURI, string localPart) { NamespaceURI = namespaceURI ?? ""; LocalPart = localPart; Prefix = ""; }
        public QName(string namespaceURI, string localPart, string prefix) { NamespaceURI = namespaceURI ?? ""; LocalPart = localPart; Prefix = prefix ?? ""; }
        public string GetLocalPart() => LocalPart;
        public string GetNamespaceURI() => NamespaceURI;
        public string GetPrefix() => Prefix;
        public override string ToString() => string.IsNullOrEmpty(NamespaceURI) ? LocalPart : "{" + NamespaceURI + "}" + LocalPart;
    }
}
