# OutSmart DAXon — a native C# port of Saxon-HE 12.9

**OutSmart DAXon** is a pure-managed XSLT 3.0 / XPath 3.1 / XQuery 3.1 engine for the classic
**.NET Framework (4.7.2+)** — a complete C# port of **Saxon-HE 12.9** (© Saxonica, MPL 2.0).
No Java, no IKVM at runtime: one self-contained assembly with no third-party dependencies,
for hosts that are stuck on the old Framework where current XSLT 3.0 engines are not an option.

> OutSmart DAXon is an independent derivative work. It is **not affiliated with, endorsed by, or
> supported by Saxonica Limited**. "Saxon" and "Saxonica" are trademarks of Saxonica Limited and are
> used here only descriptively, to identify the upstream project this port derives from.

**Base version**: the port derives from the Saxon-HE **12.9** Java source release, published by
Saxonica at <https://github.com/Saxonica/Saxon-HE> (see also <https://www.saxonica.com/>).

## Build

```
dotnet build OutSmart.DAXon.sln -c Release
```

builds `OutSmart.DAXon.dll` — the whole engine, one self-contained assembly
(`src/OutSmart.DAXon/`).

Note: `.gitattributes` pins `* -text` (no CRLF conversion) — the tree contains byte-sensitive
embedded data files. If your clone predates that file, run
`git config core.autocrlf false; git checkout -- .` once.

## Usage

```csharp
using System.IO;
using OutSmart.DAXon.Api;

// One Processor per process — reusable and thread-safe. Optional resource limits:
// new Processor(transformTimeout: TimeSpan.FromMinutes(1), maxInputBytes: 150L * 1024 * 1024)
var proc = new Processor();

// Compile once, reuse for any number of transformations (thread-safe).
XsltExecutable exe = proc.NewXsltCompiler()
    .Compile(new StringReader(xsltText), "urn:stylesheet");

// Parse the input document (or Build(filePath) for a file).
XdmNode input = proc.NewDocumentBuilder()
    .Build(new StringReader(inputXml), "urn:input");

// One transformer per transformation — cheap to create, never share between calls.
Xslt30Transformer tr = exe.Load30();
var output = new StringWriter();
tr.SetGlobalContextItem(input, true);
tr.ApplyTemplates(input, proc.NewSerializer(output));
string result = output.ToString();
```

Expected failures (bad stylesheet, bad input, resource limit hit) arrive as
`DAXonApiException` with standard XSLT/XPath error codes — one `try/catch` around the
transform call is the whole error-handling contract.

## Status

The port is verified against the full W3C QT3 (XPath/XQuery 3.1) + XSLT 3.0 test corpora and
matches Java Saxon-HE verdict-for-verdict, except 17 cases requiring XML 1.1 input documents,
which the .NET `XmlReader` cannot parse. Hostile inputs cannot kill the process: transformation
and compile-time deadlines, input-size caps and adaptive stack guards turn deep recursion / deep
JSON / deep regex nesting into catchable coded errors.

## License & attribution

Licensed under the **Mozilla Public License, Version 2.0** — see [`LICENSE`](LICENSE).

This is a derivative work of **Saxon-HE 12.9**, © Saxonica Limited, which is itself distributed under
the MPL 2.0 (source: <https://github.com/Saxonica/Saxon-HE>). Per-file Saxonica copyright notices are
retained as required by the license. Files modified in the course of this port carry the same MPL 2.0
terms; the modified source is published here in full. The engine has no third-party dependencies.

OutSmart DAXon is maintained by OutSmart (<https://outsmartteam.com/>) and is independent of Saxonica.
