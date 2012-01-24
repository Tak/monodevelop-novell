//
// NUnitProjectTestSuite.cs
//
// Author:
//   Lluis Sanchez Gual
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Collections.Generic;

using MonoDevelop.Projects;
using MonoDevelop.Projects.Dom;
using MonoDevelop.Projects.Dom.Parser;
using MonoDevelop.Ide;

namespace MonoDevelop.NUnit
{
	public class NUnitProjectTestSuite: NUnitAssemblyTestSuite
	{
		DotNetProject project;
		string resultsPath;
		string storeId;
		
		public NUnitProjectTestSuite (DotNetProject project): base (project.Name, project)
		{
			storeId = Path.GetFileName (project.FileName);
			resultsPath = Path.Combine (project.BaseDirectory, "test-results");
			ResultsStore = new XmlResultsStore (resultsPath, storeId);
			this.project = project;
			project.NameChanged += new SolutionItemRenamedEventHandler (OnProjectRenamed);
			IdeApp.ProjectOperations.EndBuild += new BuildEventHandler (OnProjectBuilt);
		}
		
		public static NUnitProjectTestSuite CreateTest (DotNetProject project)
		{
			foreach (ProjectReference p in project.References)
				if (p.Reference.IndexOf("nunit.framework", StringComparison.OrdinalIgnoreCase) != -1 || p.Reference.IndexOf("nunit.core", StringComparison.OrdinalIgnoreCase) != -1)
					return new NUnitProjectTestSuite (project);
			return null;
		}

		protected override SourceCodeLocation GetSourceCodeLocation (string fullClassName, string methodName)
		{
			ProjectDom ctx = ProjectDomService.GetProjectDom (project);
			IType cls = ctx.GetType (fullClassName);
			if (cls == null)
				return null;
			
			foreach (IMethod met in cls.Methods) {
				if (met.Name == methodName)
					return new SourceCodeLocation (cls.CompilationUnit.FileName, met.Location.Line, met.Location.Column);
			}
			return new SourceCodeLocation (cls.CompilationUnit.FileName, cls.Location.Line, cls.Location.Column);
		}
		
		public override void Dispose ()
		{
			project.NameChanged -= new SolutionItemRenamedEventHandler (OnProjectRenamed);
			IdeApp.ProjectOperations.EndBuild -= new BuildEventHandler (OnProjectBuilt);
			base.Dispose ();
		}
		
		void OnProjectRenamed (object sender, SolutionItemRenamedEventArgs e)
		{
			UnitTestGroup parent = Parent as UnitTestGroup;
			if (parent != null)
				parent.UpdateTests ();
		}
		
		void OnProjectBuilt (object s, BuildEventArgs args)
		{
			if (RefreshRequired)
				UpdateTests ();
		}
	
		protected override string AssemblyPath {
			get { return project.GetOutputFileName (IdeApp.Workspace.ActiveConfiguration); }
		}
		
		protected override string TestInfoCachePath {
			get { return Path.Combine (resultsPath, storeId + ".test-cache"); }
		}
		
		protected override IEnumerable<string> SupportAssemblies {
			get {
				// Referenced assemblies which are not in the gac and which are not localy copied have to be preloaded
				DotNetProject project = base.OwnerSolutionItem as DotNetProject;
				if (project != null) {
					foreach (ProjectReference pr in project.References) {
						if (pr.ReferenceType != ReferenceType.Package && !pr.LocalCopy) {
							foreach (string file in pr.GetReferencedFileNames (IdeApp.Workspace.ActiveConfiguration))
								yield return file;
						}
					}
				}
			}
		}
	}
}

