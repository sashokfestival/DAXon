////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Linked
{
    public class AttributeMapWithIdentity : IAttributeMap
    {
        private readonly IList<AttributeInfo> attributes;
        public AttributeMapWithIdentity(IList<AttributeInfo> attributes)
        {
            this.attributes = attributes;
        }

        public virtual int Size()
        {
            int count = 0;
            foreach (AttributeInfo att in attributes)
            {
                if (!(att is AttributeInfo.Deleted))
                {
                    count++;
                }
            }

            return count;
        }

        public virtual IAxisIterator IterateAttributes(ElementImpl owner)
        {
            IList<NodeInfo> list = new List<NodeInfo>(attributes.Count);
            for (int i = 0; i < attributes.Count; i++)
            {
                AttributeInfo att = attributes[i];
                if (!(att is AttributeInfo.Deleted))
                {
                    list.Add(new AttributeImpl(owner, i));
                }
            }

            return new NodeListIterator(list);
        }

        private bool IsDeleted(AttributeInfo info)
        {
            return (info is AttributeInfo.Deleted);
        }

        public virtual AttributeInfo Get(INodeName name)
        {
            foreach (AttributeInfo info in attributes)
            {
                if (info.GetNodeName().Equals(name) && !(info is AttributeInfo.Deleted))
                {
                    return info;
                }
            }

            return null;
        }

        public virtual AttributeInfo Get(NamespaceUri uri, string local)
        {
            foreach (AttributeInfo info in attributes)
            {
                INodeName name = info.GetNodeName();
                if (name.GetLocalPart().Equals(local) && name.HasURI(uri) && !(info is AttributeInfo.Deleted))
                {
                    return info;
                }
            }

            return null;
        }

        public virtual AttributeMapWithIdentity Set(int index, AttributeInfo info)
        {
            IList<AttributeInfo> newList = new List<AttributeInfo>(attributes);
            if (index >= 0 && index < attributes.Count)
            {
                newList[index] = info;
            }
            else if (index == attributes.Count)
            {
                newList.Add(info);
            }

            return new AttributeMapWithIdentity(newList);
        }

        public virtual AttributeMapWithIdentity Add(AttributeInfo info)
        {
            IList<AttributeInfo> newList = new List<AttributeInfo>(attributes);
            newList.Add(info);
            return new AttributeMapWithIdentity(newList);
        }

        public virtual AttributeMapWithIdentity Remove(int index)
        {
            IList<AttributeInfo> newList = new List<AttributeInfo>(attributes);
            if (index >= 0 && index < attributes.Count)
            {
                AttributeInfo.Deleted del = new AttributeInfo.Deleted(attributes[index]);
                newList[index] = del;
            }

            return new AttributeMapWithIdentity(newList);
        }

        public virtual AttributeInfo GetByFingerprint(int fingerprint, NamePool namePool)
        {
            foreach (AttributeInfo info in attributes)
            {
                INodeName name = info.GetNodeName();
                if (name.ObtainFingerprint(namePool) == fingerprint)
                {
                    return info;
                }
            }

            return null;
        }

        public virtual IEnumerator<AttributeInfo> IIterator()
        {
            return attributes.Where((info) => !(info is AttributeInfo.Deleted)).IIterator();
        }

        public virtual List<AttributeInfo> AsList()
        {
            IList<AttributeInfo> list = attributes.Where((info) => !(info is AttributeInfo.Deleted)).ToList();
            return list is List<object> ? (List<AttributeInfo>)list : new List<AttributeInfo>(list);
        }

        public virtual AttributeInfo ItemAt(int index)
        {
            return attributes[index];
        }
        public IEnumerator<AttributeInfo> GetEnumerator() => throw new NotImplementedException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotImplementedException();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual string GetValue(NamespaceUri uri, string local) => throw new NotImplementedException();
        public virtual string GetValue(string local) => throw new NotImplementedException();
        public virtual IAttributeMap Put(AttributeInfo att) => throw new NotImplementedException();
        public virtual IAttributeMap Remove(INodeName name) => throw new NotImplementedException();
        public virtual void Verify() { throw new NotImplementedException(); }
        public virtual IAttributeMap Apply(Func<AttributeInfo, AttributeInfo> mapper) => throw new NotImplementedException();
    }
}