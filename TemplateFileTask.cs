using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using System.Linq;

namespace MSBuild.Axantum.Tasks
{
    public class TemplateFileTask : Task
    {
        [Required]
        public string TemplateFile { get; set; }

        [Required]
        public string TargetFile { get; set; }

        [Required]
        public ITaskItem[] Values { get; set; }

        public override bool Execute()
        {
            string templateContent = File.ReadAllText(TemplateFile);

            foreach (ITaskItem item in Values)
            {
                templateContent = templateContent.Replace(item.ItemSpec, item.GetMetadata("Value"));
            }

            if (File.Exists(TargetFile) && File.ReadAllText(TargetFile) == templateContent)
            {
                return true;
            }

            File.WriteAllText(TargetFile, templateContent);
            return true;
        }
    }
}