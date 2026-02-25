
using UnityEngine;

namespace Arawn.CrystalSave.Runtime
{
	public enum LogLevel
	{
		Off = 0,
		Error = 1,
		Warning = 2,
		Info = 3,
	}

	public static class Logger
	{
		public static LogLevel LogThreshold = LogLevel.Info;
		
		// Optional config for granular logging control (only used when LogLevel is Info)
		private static LoggerConfig cachedConfig;
		private static bool configSearched = false;

		/// <summary>
		/// Standard logging method (backwards compatible)
		/// </summary>
		public static void Log(string message, LogLevel level = LogLevel.Info)
		{
			if (LogThreshold == LogLevel.Off)
			{
				return; // Logging is disabled
			}

			if (level <= LogThreshold)
			{
				switch (level)
				{
					case LogLevel.Info:
						Debug.Log(message);
						break;
					case LogLevel.Warning:
						Debug.LogWarning(message);
						break;
					case LogLevel.Error:
						Debug.LogError(message);
						break;
				}
			}
		}
		
		/// <summary>
		/// Standard logging method with context object (makes log clickable in Console)
		/// </summary>
		public static void Log(string message, Object context, LogLevel level = LogLevel.Info)
		{
			if (LogThreshold == LogLevel.Off)
			{
				return; // Logging is disabled
			}

			if (level <= LogThreshold)
			{
				switch (level)
				{
					case LogLevel.Info:
						Debug.Log(message, context);
						break;
					case LogLevel.Warning:
						Debug.LogWarning(message, context);
						break;
					case LogLevel.Error:
						Debug.LogError(message, context);
						break;
				}
			}
		}
		
		/// <summary>
		/// Logging with optional category filtering (for granular control)
		/// If LoggerConfig exists and LogLevel is Info, only enabled categories will log.
		/// Otherwise behaves like standard Log method.
		/// </summary>
		public static void Log(string message, LogCategory category, LogLevel level = LogLevel.Info)
		{
			if (LogThreshold == LogLevel.Off)
			{
				return; // Logging is disabled
			}

			// For Error and Warning, always log regardless of category
			if (level != LogLevel.Info)
			{
				Log(message, level);
				return;
			}
			
			// For Info level, check if we should filter by category
			if (level == LogLevel.Info && LogThreshold >= LogLevel.Info)
			{
				LoggerConfig config = GetLoggerConfig();
				
				// If no config exists, log everything (default behavior)
				if (config == null)
				{
					Debug.Log(message);
					return;
				}
				
				// Config exists, check if this category is enabled
				if (config.IsEnabled(category))
				{
					Debug.Log(message);
				}
			}
		}
		
		/// <summary>
		/// Logging with category filtering and context object (makes log clickable in Console)
		/// </summary>
		public static void Log(string message, Object context, LogCategory category, LogLevel level = LogLevel.Info)
		{
			if (LogThreshold == LogLevel.Off)
			{
				return; // Logging is disabled
			}

			// For Error and Warning, always log regardless of category
			if (level != LogLevel.Info)
			{
				Log(message, context, level);
				return;
			}
			
			// For Info level, check if we should filter by category
			if (level == LogLevel.Info && LogThreshold >= LogLevel.Info)
			{
				LoggerConfig config = GetLoggerConfig();
				
				// If no config exists, log everything (default behavior)
				if (config == null)
				{
					Debug.Log(message, context);
					return;
				}
				
				// Config exists, check if this category is enabled
				if (config.IsEnabled(category))
				{
					Debug.Log(message, context);
				}
			}
		}
		
		/// <summary>
		/// Get the LoggerConfig if it exists (cached for performance)
		/// </summary>
		private static LoggerConfig GetLoggerConfig()
		{
			if (!configSearched)
			{
				#if ARAWN_REMEMBERME && MEMORYPACK
				cachedConfig = CrystalSaveOverrides.GetOverride<LoggerConfig>();
				#endif
				if (cachedConfig == null)
					cachedConfig = Resources.Load<LoggerConfig>("LoggerConfig");
				configSearched = true;
			}
			return cachedConfig;
		}
		
		/// <summary>
		/// Clear cached config (useful after creating/deleting LoggerConfig in editor)
		/// </summary>
		public static void RefreshConfig()
		{
			configSearched = false;
			cachedConfig = null;
		}
	}
}
