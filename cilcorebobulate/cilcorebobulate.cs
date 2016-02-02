using System;
using System.Linq;
using System.Collections.ObjectModel;
using Mono.Cecil;

class MainClass
{


	public static int Main (string[] args)
	{
		string input_assembly = args [0], output_assembly = args [1];

		if (args.Length != 2) {
			Console.WriteLine ("cilcorebobulate from to");
			return 1;
		}

		Console.WriteLine ("A {0}...", args [1]);
		var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly (input_assembly);

		Console.WriteLine ("Reading the CoreCLR assemblies...");
		var coreclr_assemblies = new Collection<AssemblyNameReference> () {
			new Mono.Cecil.AssemblyNameReference ("System.Runtime", new Version (4, 0, 21, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.Runtime.Extensions", new Version (4, 0, 11, 0)),
			new Mono.Cecil.AssemblyNameReference ("System.Console", new Version (4, 0, 0, 0))
		};

		foreach (var core_assembly in coreclr_assemblies)
			core_assembly.PublicKeyToken = new byte[] {0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a};

		Console.WriteLine ("Processing the modules...");
		foreach (var module in assembly.Modules) {
			Console.WriteLine ("  M {0}", module.Name);
			Console.WriteLine ("  Replacing assembly references in types...");
			foreach (var type in module.GetTypeReferences ()) {
				
				Console.WriteLine ("    T {0} S {1}", type.FullName, type.Scope);
				if (type.Scope.MetadataScopeType != MetadataScopeType.AssemblyNameReference) {
					Console.WriteLine ("        Unexpected scope, skipping.");
					continue;
				}
						
				string lib;
				if (type.FullName.IndexOf ("System.Runtime.") == 0
				    || type.FullName.IndexOf ("System.Int") == 0
					|| new[]{"System.Object"}.FirstOrDefault (s => s == type.FullName) != null)
					lib = "System.Runtime";
				else if (type.FullName == "System.Math")
					lib = "System.Runtime.Extensions";
				else if (type.FullName == "System.Console")
					lib = "System.Console";
				else
					throw new System.Exception (String.Format ("mscorlib type {0} not supported!", type.FullName));

				var replacement_assembly = coreclr_assemblies.Single (a => a.Name == lib);
				if (!module.AssemblyReferences.Contains (replacement_assembly))
					module.AssemblyReferences.Add (replacement_assembly);

				type.Scope = replacement_assembly;

			}

			// Remove default mscorlib
			module.AssemblyReferences.Remove (module.AssemblyReferences.Single (a => a.Name == "mscorlib"));
		}

		assembly.Write (output_assembly);

		return 0;
	}

}
