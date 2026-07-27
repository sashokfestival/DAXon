////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XQuery
{
    /// <summary>
    /// An immutable list of function or variable annotations, or of annotation assertions
    /// </summary>
    public class AnnotationList : IEnumerable<Annotation>
    {
        /// <summary>
        /// An empty annotation list
        /// </summary>
        public static AnnotationList EMPTY = new AnnotationList(new List<Annotation>());
        private readonly IList<Annotation> list;
        /// <summary>
        /// An empty annotation list
        /// </summary>
        public AnnotationList(IList<Annotation> list)
        {
            this.list = list;
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public static AnnotationList Singleton(Annotation ann)
        {
            return new AnnotationList(new List<Annotation>(1) { ann });
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual void Check(Configuration config, string where)
        {
            Dictionary<NamespaceUri, IList<Annotation>> map = GroupByNamespace();
            foreach (KeyValuePair<NamespaceUri, IList<Annotation>> entry in map.EntrySet())
            {
                IFunctionAnnotationHandler handler = config.GetFunctionAnnotationHandler(entry.Key);
                if (handler != null)
                {
                    handler.Check(new AnnotationList(entry.Value), where);
                }
            }
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        private Dictionary<NamespaceUri, IList<Annotation>> GroupByNamespace()
        {
            Dictionary<NamespaceUri, IList<Annotation>> result = new Dictionary<NamespaceUri, IList<Annotation>>();
            foreach (Annotation ann in list)
            {
                NamespaceUri ns = ann.AnnotationQName.GetNamespaceUri();
                if (result.ContainsKey(ns))
                {
                    result.Get(ns).Add(ann);
                }
                else
                {
                    IList<Annotation> list = new List<Annotation>();
                    list.Add(ann);
                    result.Put(ns, list);
                }
            }

            return result;
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual AnnotationList FilterByNamespace(NamespaceUri ns)
        {
            IList<Annotation> @out = new List<Annotation>();
            foreach (Annotation ann in list)
            {
                if (ann.AnnotationQName.HasURI(ns))
                {
                    @out.Add(ann);
                }
            }

            return new AnnotationList(@out);
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual IEnumerator<Annotation> IIterator()
        {
            return list.IIterator();
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual bool IsEmpty()
        {
            return list.IsEmpty();
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual int Size()
        {
            return list.Count;
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual Annotation Get(int i)
        {
            return list[i];
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual bool Includes(StructuredQName name)
        {
            foreach (Annotation a in list)
            {
                if (a.AnnotationQName.Equals(name))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public virtual bool Includes(string localName)
        {
            foreach (Annotation a in list)
            {
                if (a.AnnotationQName.GetLocalPart().Equals(localName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public override bool Equals(object other)
        {

            // treat the annotation list as ordered
            return other is AnnotationList && list.Equals(((AnnotationList)other).list);
        }

        /// <summary>
        /// An empty annotation list
        /// </summary>
        public override int GetHashCode()
        {
            return list.GetHashCode();
        }
        public IEnumerator<Annotation> GetEnumerator() => list.GetEnumerator(); // StubGen NIE -> real: delegate to the backing list
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => list.GetEnumerator();
    }
}
