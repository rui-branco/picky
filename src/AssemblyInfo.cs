using System.Reflection;
using System.Runtime.InteropServices;

// csc turns these into the Win32 version resource. Without them Windows has no
// display name for the exe and falls back to the bare filename ("picky").
[assembly: AssemblyTitle("Picky")]
[assembly: AssemblyProduct("Picky")]
[assembly: AssemblyDescription("Choose which browser opens each link")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: ComVisible(false)]
