// Explicit global using directives.
// These mirror what <ImplicitUsings>enable</ImplicitUsings> injects automatically,
// but are declared here as source so they are available in all build contexts —
// including the WPF XAML compiler's auto-generated temp project (*_wpftmp.csproj)
// which does not inherit <ImplicitUsings> from the main project file.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.IO.Ports;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
