// Explicit global using directives.
// Mirrors <ImplicitUsings>enable</ImplicitUsings> so System.IO types (Path,
// Directory, File, Stream) are available in all build contexts, including the
// WPF XAML compiler's auto-generated temp project (*_wpftmp.csproj) which does
// not inherit <ImplicitUsings> from the project file.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
