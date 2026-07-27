////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Model
{
    public sealed class DocumentPool
    {
        // The document pool ensures that the document()
        // function, when called twice with the same URI, returns the same document
        // each time. For this purpose we use a hashtable from
        // URI to DocumentInfo object.
        private readonly Dictionary<DocumentKey, ITreeInfo> documentNameMap = new Dictionary<DocumentKey, ITreeInfo>(10);
        // The set of documents known to be unavailable. These documents must remain
        // unavailable for the duration of a transformation or query!
        private readonly HashSet<DocumentKey> unavailableDocuments = new HashSet<DocumentKey>(10);
        public void Add(ITreeInfo doc, string uri)
        {
            lock (this)
            {
                if (uri != null)
                {
                    Add(doc, new DocumentKey(uri));
                }
            }
        }

        public void Add(ITreeInfo doc, DocumentKey uri)
        {
            lock (this)
            {
                if (uri != null)
                {
                    ITreeInfo existing = documentNameMap.TryGetValue(uri, out var __ti3) ? __ti3 : null;
                    if (existing != null && existing != doc)
                    {
                        throw new XPathException("Cannot have two different documents with the same document-uri " + uri.AbsoluteURI);
                    }

                    documentNameMap.Put(uri, doc);
                }
            }
        }

        public ITreeInfo Find(string uri)
        {
            lock (this)
            {
                return documentNameMap.TryGetValue(new DocumentKey(uri), out var __ti1) ? __ti1 : null;
            }
        }

        public ITreeInfo Find(DocumentKey uri)
        {
            lock (this)
            {
                return documentNameMap.TryGetValue(uri, out var __ti2) ? __ti2 : null;
            }
        }

        public string GetDocumentURI(NodeInfo doc)
        {
            lock (this)
            {
                foreach (DocumentKey uri in documentNameMap.KeySet())
                {
                    ITreeInfo found = Find(uri);
                    if (found == null)
                    {
                        continue; // can happen when discard-document() is used concurrently
                    }

                    if (found.GetRootNode().Equals(doc))
                    {
                        return uri.ToString();
                    }
                }

                return null;
            }
        }

        public bool Contains(ITreeInfo doc)
        {
            lock (this)
            {

                // relies on "equals" for nodes comparing node identity
                return documentNameMap.Values().Contains(doc);
            }
        }

        public ITreeInfo Discard(ITreeInfo doc)
        {
            lock (this)
            {
                foreach (KeyValuePair<DocumentKey, ITreeInfo> e in documentNameMap.EntrySet())
                {
                    DocumentKey name = e.Key;
                    ITreeInfo entry = e.Value;
                    if (entry.Equals(doc))
                    {
                        documentNameMap.Remove(name);
                        return doc;
                    }
                }

                return doc;
            }
        }

        public void DiscardIndexes(KeyManager keyManager)
        {
            foreach (ITreeInfo doc in documentNameMap.Values())
            {
                keyManager.ClearDocumentIndexes(doc);
            }
        }

        public void MarkUnavailable(DocumentKey uri)
        {
            unavailableDocuments.Add(uri);
        }

        public bool IsMarkedUnavailable(DocumentKey uri)
        {
            return unavailableDocuments.Contains(uri);
        }
    }
}
