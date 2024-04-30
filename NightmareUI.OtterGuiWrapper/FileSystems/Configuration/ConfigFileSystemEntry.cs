using ECommons.Logging;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
public class ConfigFileSystemEntry
{
		public virtual string Path
		{
				get
				{
						PluginLog.Error($"You must override Path property of ConfigFileSystemEntry");
						throw new NotImplementedException("You must override Path property of ConfigFileSystemEntry");
				}
		}
		public virtual void Draw()
		{
				PluginLog.Error($"You must override Draw method of ConfigFileSystemEntry");
				throw new NotImplementedException("You must override Draw method of ConfigFileSystemEntry");
		}
}
