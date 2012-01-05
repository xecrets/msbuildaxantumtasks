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
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.Axantum.Tasks
{
    public class PrettyPrintXmlTask : Task
    {
        public override bool Execute()
        {
            if (SourceFiles.Length != DestinationFiles.Length)
            {
                throw new ArgumentOutOfRangeException("DestinationFiles", "Not the same number of DestinationFiles as SourceFiles");
            }
            for (int i = 0; i < SourceFiles.Length; ++i)
            {
                PrettyPrintXmlFile(SourceFiles[i], DestinationFiles[i]);
            }
            return true;
        }

        private void PrettyPrintXmlFile(string sourceFile, string destinationFile)
        {
            FileInfo sourceInfo = new FileInfo(sourceFile);
            if (!sourceInfo.Exists)
            {
                throw new ArgumentException("File not found.", "sourceFile");
            }

            XmlDocument xml = new XmlDocument();
            xml.Load(sourceFile);

            using (MemoryStream prettyOutput = new MemoryStream((int)(sourceInfo.Length + sourceInfo.Length / 10)))
            {
                PrettyPrintXml(sourceInfo, prettyOutput);

                FileInfo destinationInfo = new FileInfo(destinationFile);
                bool isEqual = IsFileIdenticalWithStream(destinationInfo, prettyOutput);

                if (!isEqual)
                {
                    WriteStreamToFileWithBackup(destinationInfo, prettyOutput);
                }
            }
        }

        private static void PrettyPrintXml(FileInfo xmlInfo, Stream prettyOutput)
        {
            using (StreamReader reader = new StreamReader(xmlInfo.FullName))
            using (XmlReader xmlReader = XmlReader.Create(reader))
            {
                TextWriter textWriter = new StreamWriter(prettyOutput, Encoding.UTF8);
                string indent = "";
                string attributeIndent = "";
                bool inhibitNewLineAtEndElement = false;
                textWriter.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>");
                while (xmlReader.Read())
                {
                    switch (xmlReader.NodeType)
                    {
                        case XmlNodeType.Attribute:
                            throw new InvalidOperationException("Attribute found out of sequence. Internal error.");
                        case XmlNodeType.CDATA:
                            break;
                        case XmlNodeType.Comment:
                            string[] comments = xmlReader.Value.Split(new char[] { (char)10 }, StringSplitOptions.None);
                            if (comments.Length == 0)
                            {
                                break;
                            }
                            string first = comments[0].Trim();
                            textWriter.WriteLine();
                            textWriter.Write("{0}<!-- {1}", indent, first);
                            string oldIndent = indent;
                            indent += "     ";
                            int commentIndent = first.LastIndexOf("  ");
                            if (commentIndent >= 0)
                            {
                                indent = indent + SpaceString(commentIndent + 2);
                            }
                            for (int i = 1; i < comments.Length; ++i)
                            {
                                textWriter.WriteLine();
                                textWriter.Write("{0}{1}", indent, comments[i].Trim());
                            }
                            textWriter.Write(" -->");
                            indent = oldIndent;
                            break;
                        case XmlNodeType.Document:
                            break;
                        case XmlNodeType.DocumentFragment:
                            break;
                        case XmlNodeType.DocumentType:
                            break;
                        case XmlNodeType.Element:
                            attributeIndent = "";
                            string element = xmlReader.Name;
                            textWriter.WriteLine();
                            textWriter.Write("{0}<{1}", indent, element);
                            int attributesLeft = xmlReader.AttributeCount;
                            while (xmlReader.MoveToNextAttribute())
                            {
                                --attributesLeft;
                                string content = String.Format("{0} {1}=\"{2}\"", attributeIndent, xmlReader.Name, HttpUtility.HtmlEncode(xmlReader.Value));
                                textWriter.Write(content);
                                if (attributesLeft > 0)
                                {
                                    textWriter.WriteLine();
                                }
                                if (String.IsNullOrEmpty(attributeIndent))
                                {
                                    attributeIndent = indent + SpaceString(element.Length + 1);
                                }
                            }
                            xmlReader.MoveToElement();
                            attributeIndent = "";
                            if (xmlReader.IsEmptyElement)
                            {
                                textWriter.Write(" />");
                            }
                            else
                            {
                                indent += "    ";
                                textWriter.Write(">");
                            }
                            break;
                        case XmlNodeType.EndElement:
                            indent = indent.Substring(0, indent.Length - 4);
                            if (!inhibitNewLineAtEndElement)
                            {
                                textWriter.WriteLine();
                                textWriter.Write("{0}", indent);
                            }
                            textWriter.Write("</{0}>", xmlReader.Name);
                            inhibitNewLineAtEndElement = false;
                            break;
                        case XmlNodeType.EndEntity:
                            break;
                        case XmlNodeType.Entity:
                            break;
                        case XmlNodeType.EntityReference:
                            break;
                        case XmlNodeType.None:
                            break;
                        case XmlNodeType.Notation:
                            break;
                        case XmlNodeType.ProcessingInstruction:
                            break;
                        case XmlNodeType.SignificantWhitespace:
                            break;
                        case XmlNodeType.Text:
                            textWriter.Write(xmlReader.Value);
                            inhibitNewLineAtEndElement = true;
                            break;
                        case XmlNodeType.Whitespace:
                            break;
                        case XmlNodeType.XmlDeclaration:
                            break;
                        default:
                            break;
                    }
                }
                textWriter.WriteLine();
                textWriter.Flush();
            }
        }

        private static string SpaceString(int commentIndent)
        {
            return Enumerable.Repeat<string>(" ", commentIndent).Aggregate((a, b) => a + b);
        }

        private static void PrettyPrintXml2(XmlDocument xml, Stream prettyOutput)
        {
            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Encoding = Encoding.UTF8;
            writerSettings.Indent = true;
            writerSettings.IndentChars = "    ";
            writerSettings.NewLineChars = Environment.NewLine;
            writerSettings.NewLineHandling = NewLineHandling.Replace;
            writerSettings.NewLineOnAttributes = true;
            writerSettings.OmitXmlDeclaration = false;

            using (XmlWriter destinationWriter = XmlWriter.Create(prettyOutput, writerSettings))
            {
                xml.WriteContentTo(destinationWriter);
            }
        }

        private static bool IsFileIdenticalWithStream(FileInfo fileInfo, MemoryStream stream)
        {
            if (!fileInfo.Exists || fileInfo.Length != stream.Length)
            {
                return false;
            }
            byte[] newContent = stream.GetBuffer();
            byte[] currentContent = new byte[fileInfo.Length];
            using (FileStream destinationStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
            {
                destinationStream.Read(currentContent, 0, currentContent.Length);
            }
            for (int i = 0; i < currentContent.Length; ++i)
            {
                if (newContent[i] != currentContent[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteStreamToFileWithBackup(FileInfo fileInfo, MemoryStream stream)
        {
            FileInfo tempInfo = new FileInfo(fileInfo.FullName + ".bak");
            try
            {
                if (fileInfo.Exists)
                {
                    if (tempInfo.Exists)
                    {
                        tempInfo.Delete();
                    }
                    File.Move(fileInfo.FullName, tempInfo.FullName);
                }
                using (FileStream destinationStream = new FileStream(fileInfo.FullName, FileMode.CreateNew, FileAccess.Write))
                {
                    destinationStream.Write(stream.GetBuffer(), 0, (int)stream.Length);
                }
            }
            finally
            {
                tempInfo.Refresh();
                fileInfo.Refresh();
                if (tempInfo.Exists && !fileInfo.Exists)
                {
                    tempInfo.MoveTo(fileInfo.FullName);
                }
            }
        }

        [Required]
        public string[] SourceFiles { get; set; }

        [Required]
        public string[] DestinationFiles { get; set; }
    }
}