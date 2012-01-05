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
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Axantum.Tasks
{
    public class GuidFromStringTask : Task
    {
        public override bool Execute()
        {
            SHA256 sha256 = SHA256.Create();

            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(SourceString));

            byte[] shortHash = new byte[16]; ;
            Array.Copy(hash, shortHash, shortHash.Length);

            Guid resultGuid = new Guid(shortHash);

            ResultGuid = new string[] { resultGuid.ToString("B") };

            return true;
        }

        [Required]
        public string SourceString { get; set; }

        [Output]
        public string[] ResultGuid { get; set; }
    }
}