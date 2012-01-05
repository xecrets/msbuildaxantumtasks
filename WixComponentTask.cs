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
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Axantum.Tasks
{
    public class WixComponentTask : Task
    {
        private class SimpleStringTree
        {
            private SortedDictionary<string, SimpleStringTree> m_children = new SortedDictionary<string, SimpleStringTree>();

            public IDictionary<string, SimpleStringTree> Children
            {
                get
                {
                    return m_children;
                }
            }

            public string Leaf { get; set; }

            public void Add(IEnumerable<string> strings, string leafValue)
            {
                SimpleStringTree current = this;
                foreach (string s in strings)
                {
                    SimpleStringTree child;
                    if (!current.Children.TryGetValue(s, out child))
                    {
                        child = new SimpleStringTree();
                        current.Children.Add(s, child);
                    }
                    current = child;
                }
                current.Leaf = leafValue;
            }
        }

        private const string m_ns = "http://schemas.microsoft.com/wix/2006/wi";
        private XmlDocument m_doc;
        private List<string> m_components = new List<string>();

        private XmlElement CreateElement(string name)
        {
            return m_doc.CreateElement(name, m_ns);
        }

        private XmlNode BuildXmlDeclaration()
        {
            XmlDeclaration xmlDeclaration = m_doc.CreateXmlDeclaration("1.0", "utf-8", null);

            return xmlDeclaration;
        }

        private XmlNode BuildWixElement()
        {
            XmlElement wixElement = CreateElement("Wix");

            wixElement.AppendChild(BuildComponentsFragmentElement());
            wixElement.AppendChild(BuildComponentGroupFragmentElement());

            return wixElement;
        }

        private XmlNode BuildComponentGroupFragmentElement()
        {
            XmlElement wixFragment = CreateElement("Fragment");

            wixFragment.AppendChild(BuildComponentGroupElement());

            return wixFragment;
        }

        private XmlNode BuildComponentGroupElement()
        {
            XmlElement wixComponentGroup = CreateElement("ComponentGroup");
            wixComponentGroup.SetAttribute("Id", ComponentGroupId);

            foreach (string component in m_components)
            {
                XmlElement wixComponentRef = CreateElement("ComponentRef");
                wixComponentRef.SetAttribute("Id", component);

                wixComponentGroup.AppendChild(wixComponentRef);
            }

            return wixComponentGroup;
        }

        private XmlNode BuildComponentsFragmentElement()
        {
            XmlElement wixFragment = CreateElement("Fragment");

            wixFragment.AppendChild(BuildDirectoryRefElement());

            return wixFragment;
        }

        private XmlNode BuildDirectoryRefElement()
        {
            XmlElement wixDirectoryRef = CreateElement("DirectoryRef");
            wixDirectoryRef.SetAttribute("Id", DirectoryRefId);

            BuildDirectoryElements(String.Empty, GetFileTree(), wixDirectoryRef);

            return wixDirectoryRef;
        }

        private XmlElement BuildComponentElement()
        {
            XmlElement wixComponent = CreateElement("Component");

            return wixComponent;
        }

        private XmlNode BuildCreateFolderElement()
        {
            XmlElement wixCreateFolder = CreateElement("CreateFolder");

            return wixCreateFolder;
        }

        private XmlNode BuildRemoveFolderElement()
        {
            XmlElement wixRemoveFolder = CreateElement("RemoveFolder");
            wixRemoveFolder.SetAttribute("Id", "rem" + Guid.NewGuid().ToString("N"));
            wixRemoveFolder.SetAttribute("On", "uninstall");

            return wixRemoveFolder;
        }

        private SimpleStringTree GetFileTree()
        {
            string baseDirectory = Path.GetFullPath(BaseDirectory);
            if (!baseDirectory.EndsWith("\\"))
            {
                baseDirectory += "\\";
            }

            SimpleStringTree stringTree = new SimpleStringTree();

            foreach (ITaskItem taskItem in ComponentFiles)
            {
                string filePath = Path.GetFullPath(taskItem.ItemSpec);
                if (!filePath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(String.Format("{0} is not below {1}", filePath, baseDirectory));
                }
                string relativeFilePath = filePath.Substring(baseDirectory.Length);

                string[] pathParts = relativeFilePath.Split('\\');
                stringTree.Add(pathParts, filePath);
            }

            return stringTree;
        }

        private void BuildDirectoryElements(string treePath, SimpleStringTree stringTree, XmlNode parentElement)
        {
            XmlElement componentElement = BuildComponentElement();

            foreach (KeyValuePair<string, SimpleStringTree> kvp in stringTree.Children)
            {
                // Check if it's a file
                if (kvp.Value.Leaf != null)
                {
                    XmlNode fileElement = BuildFileElement(kvp.Key, kvp.Value.Leaf);
                    componentElement.AppendChild(fileElement);
                }
                else
                {
                    string nextTreePath = treePath + "/" + kvp.Key;
                    XmlNode directoryElement = BuildDirectoryElement(nextTreePath, kvp.Key);
                    BuildDirectoryElements(nextTreePath, kvp.Value, directoryElement);
                    parentElement.AppendChild(directoryElement);
                }
            }

            // Do we have any files collected?
            if (componentElement.ChildNodes.Count > 0)
            {
                // Make components use the same guid as long as the UpgradeCode, registry key and directory path is constant.
                string guid = GuidFromString(UpgradeCode + "/" + RegistryKey + "/" + "cmp" + "/" + treePath).ToString("N");
                string id = "cmp" + guid;

                componentElement.SetAttribute("Id", id);
                componentElement.SetAttribute("Guid", guid);
                componentElement.SetAttribute("Shared", "no");
                componentElement.SetAttribute("DiskId", "1");
                m_components.Add(id);

                if (parentElement.LocalName == "Directory")
                {
                    componentElement.AppendChild(BuildCreateFolderElement());
                    componentElement.AppendChild(BuildRemoveFolderElement());
                }
                componentElement.AppendChild(BuildRegistryKeyElement(id + "Installed"));

                parentElement.AppendChild(componentElement);
            }
        }

        private XmlNode BuildDirectoryElement(string treePath, string directoryName)
        {
            XmlElement directoryElement = CreateElement("Directory");
            // Make components use the same guid as long as the UpgradeCode, registry key and directory path is constant.
            string guid = GuidFromString(UpgradeCode + "/" + RegistryKey + "/" + "dir" + "/" + treePath).ToString("N");
            directoryElement.SetAttribute("Id", "dir" + guid);
            directoryElement.SetAttribute("Name", directoryName);

            return directoryElement;
        }

        private XmlNode BuildFileElement(string fileName, string filePath)
        {
            XmlElement fileElement = CreateElement("File");
            fileElement.SetAttribute("Id", "fil" + Guid.NewGuid().ToString("N"));
            fileElement.SetAttribute("Source", filePath);

            return fileElement;
        }

        private XmlNode BuildRegistryKeyElement(string name)
        {
            XmlElement wixRegistryKey = CreateElement("RegistryKey");
            string[] registryPaths = RegistryKey.Split(new char[] { '\\' }, 2);
            wixRegistryKey.SetAttribute("Root", registryPaths[0]);
            wixRegistryKey.SetAttribute("Key", registryPaths[1]);
            wixRegistryKey.SetAttribute("Action", "createAndRemoveOnUninstall");

            wixRegistryKey.AppendChild(BuildRegistryValueElement(name));

            return wixRegistryKey;
        }

        private XmlNode BuildRegistryValueElement(string name)
        {
            XmlElement wixRegistryValue = CreateElement("RegistryValue");
            wixRegistryValue.SetAttribute("Name", name);
            wixRegistryValue.SetAttribute("Value", "1");
            wixRegistryValue.SetAttribute("Type", "integer");
            wixRegistryValue.SetAttribute("KeyPath", "yes");

            return wixRegistryValue;
        }

        public override bool Execute()
        {
            if (ComponentFiles.Length == 0)
            {
                throw new InvalidOperationException("ComponentFiles");
            }

            m_doc = new XmlDocument();
            m_doc.AppendChild(BuildXmlDeclaration());
            m_doc.AppendChild(BuildWixElement());

            XmlWriterSettings xmlSettings = new XmlWriterSettings();
            xmlSettings.CloseOutput = true;
            xmlSettings.ConformanceLevel = ConformanceLevel.Document;
            xmlSettings.Encoding = Encoding.UTF8;
            xmlSettings.Indent = true;
            xmlSettings.IndentChars = "  ";
            xmlSettings.NewLineOnAttributes = false;

            using (XmlWriter writer = XmlWriter.Create(OutputFile.ItemSpec, xmlSettings))
            {
                m_doc.Save(writer);
            }

            return true;
        }

        private Guid GuidFromString(string s)
        {
            SHA256 sha256 = SHA256.Create();

            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(s));

            byte[] shortHash = new byte[16];
            for (int i = 0; i < hash.Length; ++i)
            {
                shortHash[i % shortHash.Length] ^= hash[i];
            }

            Guid resultGuid = new Guid(shortHash);

            return resultGuid;
        }

        [Required]
        public ITaskItem[] ComponentFiles { get; set; }

        [Required]
        public ITaskItem OutputFile { get; set; }

        [Required]
        public string BaseDirectory { get; set; }

        [Required]
        public string DirectoryRefId { get; set; }

        [Required]
        public string ComponentGroupId { get; set; }

        [Required]
        public string RegistryKey { get; set; }

        [Required]
        public string UpgradeCode { get; set; }
    }
}