////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Events
{
    public abstract class Builder : IReceiver
    {
        /// <summary>
        /// Constant denoting a request for the default tree model
        /// </summary>
        public const int UNSPECIFIED_TREE_MODEL = -1;
        /// <summary>
        /// Constant denoting the "linked tree" in which each node is represented as an object
        /// </summary>
        public const int LINKED_TREE = 0;
        /// <summary>
        /// Constant denoting the "tiny tree" in which the tree is represented internally using arrays of integers
        /// </summary>
        public const int TINY_TREE = 1;
        /// <summary>
        /// Constant denoting the "tiny tree" in which the tree is represented internally using arrays of integers
        /// </summary>
        public const int TINY_TREE_CONDENSED = 2;
        /// <summary>
        /// Constant denoting the "tiny tree" in which the tree is represented internally using arrays of integers
        /// </summary>
        public const int JDOM_TREE = 3;
        /// <summary>
        /// Constant denoting the "tiny tree" in which the tree is represented internally using arrays of integers
        /// </summary>
        public const int JDOM2_TREE = 4;
        /// <summary>
        /// Constant denoting the "tiny tree" in which the tree is represented internally using arrays of integers
        /// </summary>
        public const int AXIOM_TREE = 5;
        /// <summary>
        /// Constant denoting the "tiny tree" in which the tree is represented internally using arrays of integers
        /// </summary>
        public const int DOMINO_TREE = 6;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        public const int MUTABLE_LINKED_TREE = 7;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected PipelineConfiguration pipe;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected Configuration config;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected NamePool namePool;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected string systemId;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected string baseURI;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected bool uniformBaseURI = true;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected NodeInfo currentRoot;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected bool lineNumbering = false;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected bool useEventLocation = true;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected Durability durability = Durability.LASTING;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected bool started = false;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected bool timing = false;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        protected bool opened = false;
        /// <summary>
        /// Constant denoting the "mutable linked tree" in which each node is represented as an object
        /// </summary>
        private long startTime;

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual string BaseURI
        {
            get => baseURI; set
            {
                this.baseURI = value;
            }
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual NodeInfo CurrentRoot => currentRoot;
        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public Builder()
        {
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public Builder(PipelineConfiguration pipe)
        {
            this.pipe = pipe;
            config = pipe.GetConfiguration();
            lineNumbering = config.IsLineNumbering();
            namePool = config.GetNamePool();
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            this.pipe = pipe;
            config = pipe.GetConfiguration();
            lineNumbering = lineNumbering || config.IsLineNumbering();
            namePool = config.GetNamePool();
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual PipelineConfiguration GetPipelineConfiguration()
        {
            return pipe;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual BuilderMonitor GetBuilderMonitor()
        {
            return null;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void SetUseEventLocation(bool useEventLocation)
        {
            this.useEventLocation = useEventLocation;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual bool IsUseEventLocation()
        {
            return useEventLocation;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual string GetSystemId()
        {
            return systemId;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void SetDurability(Durability durability)
        {
            this.durability = durability;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual Durability GetDurability()
        {
            return durability;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void SetLineNumbering(bool lineNumbering)
        {
            this.lineNumbering = lineNumbering;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void SetTiming(bool on)
        {
            timing = on;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual bool IsTiming()
        {
            return timing;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void Open()
        {
            if (timing && !opened)
            {
                string sysId = GetSystemId();
                if (sysId == null)
                {
                    sysId = "(unknown systemId)";
                }

                GetConfiguration().Logger.Info("Building tree for " + sysId + " using " + GetType());
                startTime = (DateTime.Now.Ticks * 100L);
            }

            opened = true;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void Dispose()
        {
            if (timing && opened)
            {
                long endTime = (DateTime.Now.Ticks * 100L);
                Logger logger = GetConfiguration().Logger;
                logger.Info("Tree built in " + Timer.ShowExecutionTimeNano(endTime - startTime));
                if (currentRoot is TinyDocumentImpl)
                {
                    ((TinyDocumentImpl)currentRoot).ShowSize(logger);
                }

                startTime = endTime;
            }

            opened = false;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual bool UsesTypeAnnotations()
        {
            return true;
        }

        /// <summary>
        /// Create a Builder and initialise variables
        /// </summary>
        public virtual void Reset()
        {
            systemId = null;
            baseURI = null;
            currentRoot = null;
            lineNumbering = false;
            started = false;
            timing = false;
            opened = false;
        }
        public virtual void StartDocument(int arg0) => throw new NotImplementedException();
        public virtual void EndDocument() => throw new NotImplementedException();
        public virtual void SetUnparsedEntity(string arg0, string arg1, string arg2) => throw new NotImplementedException();
        public virtual void StartElement(INodeName arg0, ISchemaType arg1, IAttributeMap arg2, NamespaceMap arg3, ILocation arg4, int arg5) => throw new NotImplementedException();
        public virtual void EndElement() => throw new NotImplementedException();
        public virtual void Characters(UnicodeString arg0, ILocation arg1, int arg2) => throw new NotImplementedException();
        public virtual void ProcessingInstruction(string arg0, UnicodeString arg1, ILocation arg2, int arg3) => throw new NotImplementedException();
        public virtual void Comment(UnicodeString arg0, ILocation arg1, int arg2) => throw new NotImplementedException();
        // Phase 5: IReceiver Append default stubs.
        public virtual void Append(IItem item) { }
        public virtual void Append(IItem item, ILocation locationId, int copyNamespaces) { }
        public virtual bool HandlesAppend() => false;
    }
}
