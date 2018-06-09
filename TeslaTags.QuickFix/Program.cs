﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using TagLib;

namespace TeslaTags.QuickFix
{
	public static class Program
	{
		public static void Main(String[] args)
		{
			if( args.Length < 2 )
			{
				Console.WriteLine( "Usage: TeslaTags.QuickFix.exe <directory> <operation> [<value>]" );
				
				Console.WriteLine();
				Console.WriteLine( "Example: TeslaTags.QuickFix.exe . auto-trackNumbers" );
				Console.WriteLine( "\tSets the track numbers based on the filename.");

				Console.WriteLine();
				Console.WriteLine( "Example: TeslaTags.QuickFix.exe . set-albumArt Image.png" );
				Console.WriteLine( "\tSets the album-art to Image.png.");

				Console.WriteLine();
				Console.WriteLine( "Example: TeslaTags.QuickFix.exe . remove-ape" );
				Console.WriteLine( "\tRemoves APE tags.");

				return;
			}

			String directoryPath = args[0];
			if( directoryPath == "." ) directoryPath = Environment.CurrentDirectory;

			String operation = args[1];
			String value = args.Length >= 3 ? args[2] : null;

			////////////////////////////

			MainInner( directoryPath, operation, value );

			Console.WriteLine("Completed. Press [Return] to exit.");
			Console.ReadLine();
		}

		private static void MainInner( String directoryPath, String operation, String value )
		{
			List<Message> messages = new List<Message>();
			List<LoadedFile> files = Folder.LoadFiles( directoryPath, messages );
			try
			{
				foreach( Message msg in messages )
				{
					Console.WriteLine( msg.ToString() );
				}

				switch( operation.ToUpperInvariant() )
				{
					case "AUTO-TRACKNUMBERS":

						AutoTrackNumbers( files );
						break;

					case "SET-ALBUMART":

						SetAlbumArt( directoryPath, files, value );
						break;

					case "REMOVE-APE":

						RemoveApe( files );
						break;;
				}
			}
			finally
			{
				foreach( LoadedFile file in files )
				{
					if( file.IsModified )
					{
						file.AudioFile.Save();
					}
					file.AudioFile.Dispose();
					file.Dispose();
				}
			}
		}

		private static readonly Regex _fileNameTrackNumberRegex = new Regex( @"^(\d+)", RegexOptions.Compiled );

		private static void AutoTrackNumbers( List<LoadedFile> files )
		{
			Console.WriteLine( "Confirm reset of track numbers. [A] All. [Y] Only missing. [N]. No." );
			String option = Console.ReadLine().ToUpperInvariant();
			Boolean all;
			if( ( all = ( option == "A" ) ) || option == "Y" )
			{
				foreach( LoadedFile file in files )
				{
					if( file.Id3v2Tag.Track == 0 || all )
					{
						Match fileNameMatch = _fileNameTrackNumberRegex.Match( file.FileInfo.Name );
						if( !fileNameMatch.Success )
						{
							Console.WriteLine( "Couldn't match {0}", file.FileInfo.Name );
						}
						else
						{
							UInt32 oldTrackNumber = file.Id3v2Tag.Track;

							Int32 trackNumber = Int32.Parse( fileNameMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture );
							file.Id3v2Tag.Track = (UInt32)trackNumber;
							file.IsModified = true;

							Console.WriteLine( "Updated \"{0}\". Track number {1} is now {2}.", file.FileInfo.Name, oldTrackNumber, file.Id3v2Tag.Track );
						}
					}
				}
			}
		}

		private static void SetAlbumArt( String directoryPath, List<LoadedFile> files, String imageFileName )
		{
			if( String.IsNullOrWhiteSpace( imageFileName ) )
			{
				Console.WriteLine( "Image file name not specified." );
				return;
			}

			if( !Path.IsPathRooted( imageFileName ) ) imageFileName = Path.Combine( directoryPath, imageFileName );

			if( !System.IO.File.Exists( imageFileName ) )
			{
				Console.WriteLine( "Cannot find \"{0}\"", imageFileName );
				return;
			}

			IPicture newPicture = new Picture( imageFileName );

			Console.WriteLine( "Confirm reset of album art. [A] All. [Y] Only missing. [N]. No." );
			String option = Console.ReadLine().ToUpperInvariant();
			Boolean all;
			if( ( all = ( option == "A" ) ) || option == "Y" )
			{
				foreach( LoadedFile file in files )
				{
					IPicture[] pictures = file.Id3v2Tag.Pictures;
					if( pictures == null || pictures.Length == 0 || all )
					{
						// https://stackoverflow.com/questions/7237346/having-trouble-writing-artwork-with-taglib-sharp-2-0-4-0-in-net
						if( pictures == null ) pictures = new IPicture[0];
						Array.Resize( ref pictures, pictures.Length + 1 );
						Int32 idx = pictures.GetUpperBound(0);
						pictures[idx] = newPicture;

						file.Id3v2Tag.Pictures = pictures;
						file.IsModified = true;

						Console.WriteLine( "Updated \"{0}\". Now has {1} pictures.", file.FileInfo.Name, pictures.Length );
					}
					else
					{
						Console.WriteLine( "Skipped \"{0}\". Already has {1} pictures.", file.FileInfo.Name, pictures.Length );
					}
				}
			}
		}

		private static void RemoveApe( List<LoadedFile> files )
		{
			foreach( LoadedFile file in files )
			{
				TagLib.Ape.Tag apeTag = (TagLib.Ape.Tag)file.AudioFile.GetTag(TagTypes.Ape);
				if( apeTag != null )
				{
					file.AudioFile.RemoveTags( TagTypes.Ape );
					file.IsModified = true;
					Console.WriteLine( "Removed APE tag from \"{0}\".", file.FileInfo.Name );
				}
			}
		}
	}
}
