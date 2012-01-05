#region Coypright and License

/*
 * MSBuild.Axantum.Tasks - Copyright 2011, Svante Seleborg, All Rights Reserved
 *
 * This file is part of MSBuild.Axantum.Tasks.
 *
 * MSBuild.Axantum.Tasks is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * MSBuild.Axantum.Tasks is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with MSBuild.Axantum.Tasks.  If not, see <http://www.gnu.org/licenses/>.
 *
 * The source is maintained at http://msbuildaxantumtasks.codeplex.com/ please visit for
 * updates, contributions and contact with the author. You may also visit
 * http://www.axantum.com for more information about the author.
*/

#endregion Coypright and License

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Axantum.Tasks
{
    public class LatestTfsChangesetTask : Task
    {
        public override bool Execute()
        {
            if (!Directory.Exists(LocalPath) && !File.Exists(LocalPath))
            {
                throw new ArgumentException("LocalPath");
            }

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = TfsExecutablePath;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.Arguments = String.Format(@"history /format:brief /noprompt /stopafter:1 /recursive /version:T ""{0}""", LocalPath);
            psi.CreateNoWindow = true;

            string resultLine;
            using (Process p = Process.Start(psi))
            {
                using (StreamReader sr = p.StandardOutput)
                {
                    sr.ReadLine();
                    sr.ReadLine();
                    resultLine = sr.ReadLine();
                }
                p.WaitForExit();
            }
            Regex regex = new Regex(@"^(?<changeset>[0-9]+)");
            Match m = regex.Match(resultLine);
            if (!m.Success)
            {
                throw new InvalidOperationException("Unexpected output format from TF Command Line.");
            }
            long changeset = Int64.Parse(m.Result("${changeset}"), CultureInfo.InvariantCulture);

            Changeset = new string[] { changeset.ToString() };

            return true;
        }

        [Required]
        public string LocalPath { get; set; }

        [Required]
        public string TfsExecutablePath { get; set; }

        [Output]
        public string[] Changeset { get; set; }
    }
}