using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightmareUI.OtterGuiWrapper.FileSystems.Configuration;
public static class ConfigFileSystemHelpers
{
		public static IEnumerable<T?> CreateInstancesOf<T>()
		{
				return typeof(T).Assembly.GetTypes().Where(x => !x.IsAbstract && typeof(T).IsAssignableFrom(x)).Select(x => (T?)Activator.CreateInstance(x));
		}
}
