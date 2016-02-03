using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Mono.Cecil;

class MainClass
{
	static int Usage ()
	{
		Console.WriteLine ("cilcorebobulate [-c dir] [-v] from to");
		return 1;
	}

	public static int Main (string[] args)
	{
		string coreclr_dir = ".";
		string input_assembly_name = null;
		string output_assembly_name = null;
		bool verbose = false;

		for (int i = 0; i < args.Length; i++) {
			switch (args [i]) {
			case "--coreclr":
			case "-c":
				coreclr_dir = args [++i];
				break;
			case "-v":
				verbose = true;
				break;
			case "--":
				i++;
				goto default;
			default:	
				input_assembly_name = args [i++];
				output_assembly_name = args [i];
				if (i != args.Length - 1)
					return Usage ();
				break;
			}
		}

		if (verbose)
			Console.WriteLine ("Converting {0} => {1}", input_assembly_name, output_assembly_name);

		if (input_assembly_name == null || output_assembly_name == null)
			return Usage ();

		// Step 1.
		// Process all CoreCLR assemblies, build a type=>assembly mapping.

		if (verbose)
			Console.WriteLine ("Scanning the CoreCLR libraries at {0}...", coreclr_dir);

		var coreclr_dlls = Directory.GetFiles (coreclr_dir, "System.*.dll");

		var type2as = new Dictionary<string, string> ();

		foreach (var coreclr_dll in coreclr_dlls) {
//			if (coreclr_dll.IndexOf (".Private.") != -1)
//				continue;
			
			if (verbose)
				Console.WriteLine ("  A {0}", coreclr_dll);

			var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly (coreclr_dll);

			foreach (var module in assembly.Modules) {
				
				if (verbose)
					Console.WriteLine ("    M {0}", module.Name);
				
				foreach (var type in module.ExportedTypes) {
					
					if (verbose)
						Console.WriteLine ("      T {0}", type.FullName);
					
					if (type2as.ContainsKey (type.FullName)) {
						Console.WriteLine ("      Warning: Type {0} already exists in another assembly {1}", type.FullName, type2as [type.FullName]);
						// threw new Exception(String.Format ("Type {0} already exists in another assembly {1}", type.FullName, type2as [type.FullName]));
					}
					type2as [type.FullName] = assembly.FullName;
				}
			}
		}

		// Step 2.

		Console.WriteLine ("A {0}...", args [1]);
		var input_assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly (input_assembly_name);

		// You dummy, just analyze coreclr's libararies with cecil also...

		Console.WriteLine ("Reading the CoreCLR assemblies...");
		var coreclr_assemblies = new Collection<AssemblyNameReference> () {
			new Mono.Cecil.AssemblyNameReference ("System.Runtime", new Version (4, 0, 21, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.Runtime.Extensions", new Version (4, 0, 11, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.Console", new Version (4, 0, 0, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.IO", new Version (4, 0, 0, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.IO.FileSystem", new Version (4, 0, 0, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.IO.FileSystem.Primitives", new Version (4, 0, 0, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.Collections", new Version (4, 0, 0, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.ComponentModel.TypeConverter", new Version (4, 0, 0, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.Drawing.Primitives", new Version (4, 0, 0, 0))
		};

		foreach (var core_assembly in coreclr_assemblies)
			core_assembly.PublicKeyToken = new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a };

		Console.WriteLine ("Processing the modules...");
		foreach (var module in input_assembly.Modules) {
			Console.WriteLine ("  M {0}", module.Name);
			Console.WriteLine ("  Replacing assembly references in types...");
			foreach (var type in module.GetTypeReferences ()) {
				
				Console.WriteLine ("    T {0} S {1}", type.FullName, type.Scope);
				if (type.Scope.MetadataScopeType != MetadataScopeType.AssemblyNameReference) {
					Console.WriteLine ("        Unexpected scope, skipping.");
					continue;
				}
						
				string lib;
				if (type.FullName.IndexOf ("System.") == -1) {
					Console.WriteLine ("        Not in mscorlib, skipping");
					continue;
				}
				if (type.FullName.IndexOf ("System.Runtime.") == 0
				    || type.FullName.IndexOf ("System.Text.") == 0
				    || new[] {
					"System.Type",
					"System.ValueType",
					"System.RuntimeTypeHandle",
					"System.RuntimeFieldHandle",
					"System.Object",
					"System.Attribute",
					"System.Int16",
					"System.Int32",
					"System.Int64",
					"System.UInt16",
					"System.UInt32",
					"System.UInt64",
					"System.Double",
					"System.Enum",
					"System.Byte",
					"System.Char",
					"System.String",
					"System.Array",
					"System.IDisposable",
					"System.WeakReference",
					"System.Exception",
					"System.ArgumentException",
					"System.ArgumentNullException",
					"System.InvalidOperationException",
					"System.EventHandler`1"
				}
					.FirstOrDefault (s => s == type.FullName) != null)
					lib = "System.Runtime";
				else if (type.FullName == "System.Math"
				         || type.FullName.IndexOf ("System.Diagnostics.") == 0)
					lib = "System.Runtime.Extensions";
				else if (type.FullName == "System.Console")
					lib = "System.Console";
				else if (new[] { "System.IO.File", "System.IO.FileStream", "System.IO.DirectoryInfo" }.FirstOrDefault (s => s == type.FullName) != null)
					lib = "System.IO.FileSystem";
				else if (new[] { "System.IO.FileMode", "System.IO.FileAccess", "System.IO.FileShare" }.FirstOrDefault (s => s == type.FullName) != null)
					lib = "System.IO.FileSystem.Primitives";
				else if (new[] {
					"System.IO.TextWriter",
					"System.IO.TextReader",
					"System.IO.Stream",
					"System.IO.StreamReader",
					"System.IO.StreamWriter"
				}
					.FirstOrDefault (s => s == type.FullName) != null)
					lib = "System.IO";
				else if (type.FullName.IndexOf ("System.Collections.Generic.") == 0) {
					lib = "System.Collections";
				} else if (new[] {"System.Drawing.Point"
				}.FirstOrDefault (s => s == type.FullName) != null)
					lib = "System.Drawing.Primitives";
				else if (type.FullName == "System.ComponentModel.TypeConverter")
					lib = "System.ComponentModel.TypeConverter";
				else if (new[] { "System.Drawing.Bitmap" }.FirstOrDefault (s => s == type.FullName) != null) {
					Console.WriteLine ("Error: type {0} not present in .NET Core, straightforward converstion impossible.");
					return 2;
				} else
					throw new System.Exception (String.Format ("mscorlib type {0} not supported!", type.FullName));

				var replacement_assembly = coreclr_assemblies.Single (a => a.Name == lib);
				if (!module.AssemblyReferences.Contains (replacement_assembly))
					module.AssemblyReferences.Add (replacement_assembly);

				type.Scope = replacement_assembly;

			}

			// Remove default mscorlib
			try {
				module.AssemblyReferences.Remove (module.AssemblyReferences.SingleOrDefault (a => a.Name == "mscorlib"));
			} catch (System.InvalidOperationException) {
				// Not present
			}
			;
				
		}

		input_assembly.Write (output_assembly_name);

		Console.WriteLine ("Success.");

		return 0;
	}

}
