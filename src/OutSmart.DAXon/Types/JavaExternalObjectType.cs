////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Types
{
    public class JavaExternalObjectType : ExternalObjectType
    {
        private static readonly Dictionary<System.Type, JavaExternalObjectType> cache = new Dictionary<System.Type, JavaExternalObjectType>(20);
        protected System.Type javaClass;

        public override string Name => ClassNameToQName(javaClass.GetName()).GetLocalPart();

        public override string TargetNamespace => NamespaceConstant.JAVA_TYPE;

        public override StructuredQName TypeName => ClassNameToQName(javaClass.GetName());

        public virtual System.Type JavaClass => javaClass;

        public virtual string DisplayName => "java-type:" + javaClass.GetName();
        private JavaExternalObjectType(System.Type javaClass)
        {
            this.javaClass = javaClass;
        }

        public static JavaExternalObjectType Of(System.Type javaClass)
        {
            return cache.ComputeIfAbsent(javaClass, k => new JavaExternalObjectType(k));
        }

        public override ItemType GetPrimitiveItemType()
        {
            return new JavaExternalObjectType(typeof(object));
        }

        public virtual Affinity GetRelationship(JavaExternalObjectType other)
        {
            System.Type j2 = other.javaClass;
            if (javaClass.Equals(j2))
            {
                return Affinity.SAME_TYPE;
            }
            else if (javaClass.IsAssignableFrom(j2))
            {
                return Affinity.SUBSUMES;
            }
            else if (j2.IsAssignableFrom(javaClass))
            {
                return Affinity.SUBSUMED_BY;
            }
            else if (javaClass.IsInterface || j2.IsInterface)
            {
                return Affinity.OVERLAPS; // there may be an overlap, we play safe
            }
            else
            {
                return Affinity.DISJOINT;
            }
        }

        public override bool Matches(IItem item, TypeHierarchy th)
        {
            if (item.GetGenre() == Genre.EXTERNAL)
            {
                object obj = ((ObjectValue<object>)item).GetObject();
                return javaClass.IsAssignableFrom(obj.GetType());
            }

            return false;
        }

        public override string ToString()
        {
            return ClassNameToQName(javaClass.GetName()).EQName;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return javaClass.GetHashCode();
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is JavaExternalObjectType && javaClass == ((JavaExternalObjectType)obj).javaClass;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public static string ClassNameToLocalName(string className)
        {
            return className.Replace('$', '-').Replace("[", "_-");
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public static string LocalNameToClassName(string className)
        {
            StringBuilder fsb = new StringBuilder(className.Length);
            bool atStart = true;
            for (int i = 0; i < className.Length; i++)
            {
                char c = className[i];
                if (atStart)
                {
                    if (c == '_' && i + 1 < className.Length && className[i + 1] == '-')
                    {
                        fsb.Append('[');
                        i++;
                    }
                    else
                    {
                        atStart = false;
                        fsb.Append(c == '-' ? '$' : c);
                    }
                }
                else
                {
                    fsb.Append(c == '-' ? '$' : c);
                }
            }

            return fsb.ToString();
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        /// <summary>
        /// Static method to get the QName corresponding to a Java class name
        /// </summary>
        public static StructuredQName ClassNameToQName(string className)
        {
            return new StructuredQName("jt", NamespaceUri.JAVA_TYPE, ClassNameToLocalName(className));
        }
    }
}