﻿using System;
using System.Text.RegularExpressions;

namespace TeslaTags
{
	public static class Values
	{
		internal const String VariousArtistsConst = "Various Artists";
		internal const String NoAlbumConst        = "No Album";

		public static String VariousArtists => VariousArtistsConst;
		public static String NoAlbum        => NoAlbumConst;

		public static Regex FileNameTrackNumberRegex { get; } = new Regex( @"^(\d+)", RegexOptions.Compiled );
	}
}
