////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    public class ForClausePull : TuplePull
    {
        protected TuplePull @base;
        protected ForClause forClause;
        protected IFocusIterator currentIteration;
        public ForClausePull(TuplePull @base, ForClause forClause)
        {
            this.@base = @base;
            this.forClause = forClause;
        }

        public override bool NextTuple(IXPathContext context)
        {
            while (true)
            {
                if (currentIteration == null)
                {
                    if (!@base.NextTuple(context))
                    {
                        return false;
                    }
                    currentIteration = SequenceTool.FocusTracker(forClause.GetIterator(context));
                }
                IItem next = currentIteration.Next();
                if (next != null)
                {
                    context.SetLocalVariable(forClause.RangeVariable.LocalSlotNumber, VariableValue(next));
                    if (forClause.PositionVariable != null)
                    {
                        context.SetLocalVariable(forClause.PositionVariable.LocalSlotNumber, new Int64Value(currentIteration.Position()));
                    }
                    return true;
                }
                currentIteration = null;
            }
        }

        protected virtual IGroundedValue VariableValue(IItem item)
        {
            return item;
        }

        public override void Dispose()
        {
            @base.Dispose();
            if (currentIteration != null)
            {
                currentIteration.Dispose();
            }
        }
    }
}
