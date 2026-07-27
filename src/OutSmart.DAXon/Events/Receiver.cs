////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Core;

namespace OutSmart.DAXon.Events
{
    public interface IReceiver : Result
    {
        void SetPipelineConfiguration(PipelineConfiguration pipe);
        PipelineConfiguration GetPipelineConfiguration();
        void Open();
        void StartDocument(int properties);
        void EndDocument();
        void SetUnparsedEntity(string name, string systemID, string publicID);
        void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties);
        void EndElement();
        void Characters(UnicodeString chars, ILocation location, int properties);
        void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties);
        void Comment(UnicodeString content, ILocation location, int properties);
        void Append(IItem item, ILocation locationId, int properties)
;



        void Append(IItem item)
;



        void Dispose();
        bool UsesTypeAnnotations()
;



        bool HandlesAppend()
;


    }
}
