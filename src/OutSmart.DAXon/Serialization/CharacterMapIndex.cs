////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
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
    public class CharacterMapIndex : IEnumerable<CharacterMap>
    {
        private Dictionary<StructuredQName, CharacterMap> index = new Dictionary<StructuredQName, CharacterMap>(10);
        public CharacterMapIndex()
        {
        }

        public virtual CharacterMap GetCharacterMap(StructuredQName name)
        {
            return index.Get(name);
        }

        public virtual void PutCharacterMap(StructuredQName name, CharacterMap charMap)
        {
            index.Put(name, charMap);
        }

        public virtual IEnumerator<CharacterMap> IIterator()
        {
            return index.Values.IIterator();
        }

        public virtual bool IsEmpty()
        {
            return index.IsEmpty();
        }

        public virtual CharacterMapIndex Copy()
        {
            CharacterMapIndex copy = new CharacterMapIndex();
            copy.index = new Dictionary<StructuredQName, CharacterMap>(this.index);
            return copy;
        }

        public virtual CharacterMapExpander MakeCharacterMapExpander(string useMaps, IReceiver next, SerializerFactory sf)
        {
            CharacterMapExpander characterMapExpander = null;
            IList<CharacterMap> characterMaps = new List<CharacterMap>(5);
            foreach (string expandedName in useMaps.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                StructuredQName qName = StructuredQName.FromClarkName(expandedName);
                CharacterMap map = GetCharacterMap(qName);
                if (map == null)
                {
                    throw new XPathException("Character map '" + expandedName + "' has not been defined", "SEPM0016");
                }

                characterMaps.Add(map);
            }

            if (!characterMaps.IsEmpty())
            {
                characterMapExpander = sf.NewCharacterMapExpander(next);
                if (characterMaps.Count == 1)
                {
                    characterMapExpander.SetCharacterMap(characterMaps[0]);
                }
                else
                {
                    StructuredQName name = new StructuredQName("saxon", "http://saxon.sf.net/", "combined-character-map");
                    characterMapExpander.SetCharacterMap(new CharacterMap(characterMaps, name));
                }
            }

            return characterMapExpander;
        }
        public IEnumerator<CharacterMap> GetEnumerator() => IIterator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}