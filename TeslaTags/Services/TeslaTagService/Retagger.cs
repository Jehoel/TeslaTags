﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using TagLib;

namespace TeslaTags
{
	internal static class Retagger
	{
		private static readonly Regex _startsWithDigits = new Regex( @"^\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase );

		private static Boolean ValidateFile( LoadedFile file, Boolean albumArtistRequired, Boolean albumRequired, Boolean trackNumberRequired, Boolean warnIfTrackNumberPresent, Boolean warnMissingAlbumArt, List<Message> messages )
		{
			const Boolean titleRequired = true;
			const Boolean artistRequired = true;

			Boolean isValid = true;

			if( titleRequired )
			{
				if( String.IsNullOrWhiteSpace( file.Tag.Title ) )
				{
					isValid = false;
					messages.AddFileError( file.FileInfo.FullName, "Title ID3V2 tag not set." );
				}
			}

			if( artistRequired )
			{
				if( String.IsNullOrWhiteSpace( file.Tag.FirstPerformer ) )
				{
					isValid = false;
					messages.AddFileError( file.FileInfo.FullName, "Artist ID3V2 tag not set." );
				}
			}

			// Check for APE tags:
			if( file is MpegLoadedFile mpegFile )
			{
				TagLib.Ape.Tag apeTag = (TagLib.Ape.Tag)mpegFile.MpegAudioFile.GetTag(TagTypes.Ape);
				if( apeTag != null )
				{
					messages.AddFileWarning( file.FileInfo.FullName, "File has APE tags. Tesla's MCU may be unable to play this file." );
				}
			}

			if( file.Tag.Performers?.Length > 1 ) messages.AddFileWarning( file.FileInfo.FullName, "Has multiple Artists: \"{0}\". The first value was used.", file.Tag.JoinedPerformers );

			//

			if( albumArtistRequired )
			{
				if( String.IsNullOrWhiteSpace( file.Tag.FirstAlbumArtist ) )
				{
					isValid = false;
					messages.AddFileError( file.FileInfo.FullName, "Album-Artist ID3V2 tag not set." );
				}
			}

			if( file.Tag.AlbumArtists?.Length > 1 ) messages.AddFileWarning( file.FileInfo.FullName,  "Has multiple AlbumArtists: \"{0}\". The first value was used.", file.Tag.JoinedAlbumArtists );

			//

			if( albumRequired )
			{
				if( String.IsNullOrWhiteSpace( file.Tag.Album ) )
				{
					isValid = false;
					messages.AddFileError( file.FileInfo.FullName, "Album ID3V2 tag not set." );
				}
			}

			//

			if( trackNumberRequired )
			{
				if( file.Tag.Track == 0 || file.Tag.Track > 250 )
				{
					// Only fail a track if the filename starts with a digit and the track field is missing:
					if( _startsWithDigits.IsMatch( file.FileInfo.Name ) )
					{
						isValid = false;
						messages.AddFileWarning( file.FileInfo.FullName, "Filename starts with a number, but the TrackNumber ID3V2 tag is not set or is invalid." );
					}
					else
					{
						// don't fail a track if the only thing missing is the track number. Just make it a warning.
						messages.AddFileWarning( file.FileInfo.FullName, "TrackNumber ID3V2 tag not set or invalid." );
					}
				}
			}

			if( warnIfTrackNumberPresent )
			{
				if( file.Tag.Track != 0 ) messages.AddFileWarning( file.FileInfo.FullName, "Assorted file has a track number. This program cannot currently remove track numbers. Please use another program to remove/clear this tag field." );
				
				if( file.Tag.Disc  != 0 ) messages.AddFileWarning( file.FileInfo.FullName, "Assorted file has a disc number. This program cannot currently remove disc numbers. Please use another program to remove/clear this tag field." );
			}

			//

			if( warnMissingAlbumArt )
			{
				IPicture[] art = file.Tag.Pictures;
				if( art == null || art.Length == 0 )
				{
					messages.AddFileWarning( file.FileInfo.FullName, "No Album Art." ); // TODO: Validate the name/description of the IPicture? I'm not sure how Id3v2 stores it (using a 0-255 byte enum? but there are strings too, so does it matter?)
				}
				else if( art.Length > 1 )
				{
					messages.AddFileWarning( file.FileInfo.FullName, "Multiple embedded pictures." );
				}
			}

			return isValid;
		}

		public static void RetagForArtistAlbum(List<LoadedFile> files, List<Message> messages, Boolean trackNumbersExpected)
		{
			// NOOP. Handled correctly.
			// But do file validation.

			HashSet<UInt32> uniqueDiscsAndTracks = new HashSet<UInt32>();

			foreach( LoadedFile file in files )
			{
				UInt32 discAndTrack = ( file.Tag.Disc * 100 ) + file.Tag.Track;
				if( discAndTrack != 0 )
				{
					Boolean isNewDiscAndTrack = uniqueDiscsAndTracks.Add( discAndTrack );
					if( !isNewDiscAndTrack ) messages.AddFileWarning( file.FileInfo.FullName, "Duplicate Disc {0} and Track {1} tuple.", file.Tag.Disc, file.Tag.Track );
				}

				ValidateFile( file, albumArtistRequired: true, albumRequired: true, trackNumberRequired: trackNumbersExpected, warnIfTrackNumberPresent: false, warnMissingAlbumArt: true, messages );
			}
		}

		public static void RetagForArtistAlbumWithGuestArtists(List<LoadedFile> files, List<Message> messages)
		{
			// 1. Copy Artist to Title.
			// 2. Use AlbumArtist as Artist (note that all tracks will have the same AlbumArtist value, so copy it from the first track).
			
			String albumArtist = files.First().Tag.AlbumArtists.SingleOrDefault();
			if( String.IsNullOrWhiteSpace( albumArtist ) )
			{
				messages.AddFileError( files.First().FileInfo.FullName, "File does not have an album-artist." );
				return;
			}

			foreach( LoadedFile file in files )
			{
				Boolean isValid = ValidateFile( file, albumArtistRequired: true, albumRequired: true, trackNumberRequired: true, warnIfTrackNumberPresent: false, warnMissingAlbumArt: true, messages );
				if( isValid )
				{
					String oldArtist      = file.Tag.Performers.First();
					String oldTitle       = file.Tag.Title;

					if( oldArtist != albumArtist )
					{
						String newArtist      = albumArtist;
						String newTitle       = oldArtist + " - " + oldTitle;

						TagWriter.SetArtist( file, messages, newArtist );
						TagWriter.SetTitle ( file, messages, newTitle );
					}
				}
			}
		}

		public static void RetagForCompilationAlbum(List<LoadedFile> files, List<Message> messages)
		{
			// 1. Copy Artist as Title prefix.
			// 2. Set Artist to "Various Artists"

			foreach( LoadedFile file in files )
			{
				Boolean isValid = ValidateFile( file, albumArtistRequired: false, albumRequired: true, trackNumberRequired: false /*true*/, warnIfTrackNumberPresent: false, warnMissingAlbumArt: true, messages );
				if( isValid )
				{
					String oldArtist = file.Tag.Performers.First();
					String oldTitle  = file.Tag.Title;

					if( oldArtist != Values.VariousArtistsConst )
					{
						String newArtist = Values.VariousArtistsConst;
						String newTitle  = oldArtist + " - " + oldTitle;

						TagWriter.SetArtist( file, messages, newArtist );
						TagWriter.SetTitle ( file, messages, newTitle );
					}
				}
			}
		}

		public static void RetagForAssortedFiles(List<LoadedFile> files, List<Message> messages)
		{
			// Artist and Title tags are correct as-is.
			// Clear the Album and TrackNumber tags.

			foreach( LoadedFile file in files )
			{
				Boolean isValid = ValidateFile( file, albumArtistRequired: false, albumRequired: false, trackNumberRequired: false, warnIfTrackNumberPresent: true, warnMissingAlbumArt: false, messages );
				if( isValid )
				{
					String oldAlbum = file.Tag.Album;

					if( !String.IsNullOrWhiteSpace( oldAlbum ) )
					{
						TagWriter.SetAlbum( file, messages, null );
						TagWriter.SetTrackNumber( file, messages, null );
					}
				}
			}
		}

		public static void RetagForArtistAssortedFiles(List<LoadedFile> files, List<Message> messages)
		{
			// We want it displayed in the main Artists list, which means it needs an album set. Use "No album" for those:

			foreach( LoadedFile file in files )
			{
				Boolean isValid = ValidateFile( file, albumArtistRequired: false, albumRequired: false, trackNumberRequired: false, warnIfTrackNumberPresent: true, warnMissingAlbumArt: false, messages );
				if( isValid )
				{
					String oldAlbum = file.Tag.Album;
					String newAlbum = Values.NoAlbumConst;

					if( oldAlbum != newAlbum )
					{
						TagWriter.SetAlbum( file, messages, newAlbum );
						TagWriter.SetTrackNumber( file, messages, null );
					}
				}
			}
		}

		public static void RetagForGenre(FolderType folderType, List<LoadedFile> files, GenreRules genreRules, List<Message> messages)
		{
			if( genreRules.AlwaysNoop ) return;

			if( files.Count == 0 ) return;

			String folderName = files.First().FileInfo.Directory.Name;

			foreach( LoadedFile file in files )
			{
				switch( folderType )
				{
				case FolderType.ArtistAlbumWithGuestArtists:

					Boolean isGuestArtist = ( file.Tag.GetPerformers() != file.Tag.GetAlbumArtist() );

					if( isGuestArtist )
					{
						RetagFileForGenre( file, genreRules.ArtistAlbumWithGuestArtistsAction, messages );
					}
					else
					{
						RetagFileForGenre( file, genreRules.ArtistAlbumAction, messages );
					}
					
					break;

				case FolderType.AssortedFiles:

					if( genreRules.AssortedFilesAction == AssortedFilesGenreAction.UseFolderName )
					{
						TagWriter.SetGenre( file, messages, folderName );
					}
					else
					{
						GenreAction action = (GenreAction)genreRules.AssortedFilesAction;
						RetagFileForGenre( file, action, messages );
					}
					break;

				case FolderType.CompilationAlbum:

					RetagFileForGenre( file, genreRules.CompilationAlbumAction, messages );
					break;

				case FolderType.ArtistAssorted:
					
					RetagFileForGenre( file, genreRules.ArtistAssortedAction, messages );
					break;

				case FolderType.ArtistAlbum:
				case FolderType.ArtistAlbumNoTrackNumbers:
				
					RetagFileForGenre( file, genreRules.ArtistAlbumAction, messages );
					break;

				case FolderType.Empty:
				case FolderType.Reverted:
				case FolderType.Skipped:
				case FolderType.UnableToDetermine:
				default:

					// NOOp.
					break;
				}
			}
		}

		private static void RetagFileForGenre( LoadedFile loadedFile, GenreAction action, List<Message> messages )
		{
			switch( action )
			{
			case GenreAction.Clear:

				TagWriter.SetGenre( loadedFile, messages, newGenre: null );
				break;
			
			case GenreAction.Preserve:
				// NOOP
				break;

			case GenreAction.UseArtist:

				// Get the actual artist name, NOT the current Artist tag value. This will be available in the RecoveryTag.
				// If the RecoveryTag.Artist value is empty, then the Artist tag ("Performers" property) will have it.

				// Note that TagligSharp joins performers with "; " inside `Tag.JoinedPerformers` whereas ID3 uses "/" - do this to get back "AC/DC" instead of "AC; DC".

				String originalArtist = loadedFile.RecoveryTag.Artist;

				if( String.IsNullOrWhiteSpace( originalArtist ) )
				{
					originalArtist = loadedFile.Tag.GetPerformers();
				}

				if( !String.IsNullOrWhiteSpace( originalArtist ) )
				{
					TagWriter.SetGenre( loadedFile, messages, originalArtist );
				}
				break;

			default:
				throw new ArgumentOutOfRangeException( paramName: nameof(action), actualValue: action, message: "Unrecognized enum value." );
			}
		}

		/// <summary>Returns true if any files were reverted.</summary>
		public static Boolean RetagForUndo(List<LoadedFile> files, List<Message> messages)
		{
			Boolean any = false;

			foreach( LoadedFile file in files )
			{
				TagWriter.Revert( file, messages );
				if( file.IsModified ) any = true;
			}

			return any;
		}
	}

	public enum FolderType
	{
		Empty,
		ArtistAlbum, // Includes disc folders.
		ArtistAlbumNoTrackNumbers,
		ArtistAlbumWithGuestArtists,
		ArtistAssorted, // Sets album to "No Album"
		CompilationAlbum, // Sets artist to "Various Artists"
		AssortedFiles,
		Skipped,
		UnableToDetermine,
		Reverted
	}
}
