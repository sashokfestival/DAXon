////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.

namespace OutSmart.DAXon.Types
{
    internal class FloatingPointConverter
    {
        public static string FloatToString(float f) => f.ToString();
        public static string DoubleToString(double d) => d.ToString();
        public static string AppendFloat(object sb, float f, bool forceExponent) => f.ToString();
        public static string AppendFloat(object sb, float f) => f.ToString();
        public static string AppendDouble(object sb, double d, bool forceExponent) => d.ToString();
        public static string AppendDouble(object sb, double d) => d.ToString();
        public static string ConvertDouble(double d, bool forceExponent) => d.ToString();
        public static string ConvertDouble(double d) => d.ToString();
        public static string ConvertFloat(float f, bool forceExponent) => f.ToString();
        public static string ConvertFloat(float f) => f.ToString();
    }
}
