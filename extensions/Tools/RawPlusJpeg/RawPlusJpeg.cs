/*
 * RawPlusJpeg.cs
 *
 * Author(s)
 * 	Stephane Delcroix  <stephane@delcroix.org>
 *
 * This is free software. See COPYING for details
 */

using System;
using System.Collections.Generic;

using Gtk;

using FSpot;
using FSpot.UI.Dialog;
using FSpot.Extensions;

namespace RawPlusJpegExtension
{
	public class RawPlusJpeg : ICommand
	{
		public void Run (object o, EventArgs e)
		{
			Console.WriteLine ("EXECUTING RAW PLUS JPEG EXTENSION");

			if (ResponseType.Ok != HigMessageDialog.RunHigConfirmation (
				App.Instance.Organizer.Window,
				DialogFlags.DestroyWithParent,
				MessageType.Warning,
				"Merge Raw+Jpegs",
				"This operation will merge Raw and Jpegs versions of the same image as one unique image. The Raw image will be the Original version, the jpeg will be named 'Jpeg' and all subsequent versions will keep their original names (if possible).\n\nNote: only enabled for some formats right now.",
				"Do it now"))
				return;

			Photo [] photos = App.Instance.Database.Photos.Query ((Tag [])null, null, null, null);
			Array.Sort (photos, new IBrowsableItemComparer.CompareDirectory ());

			Photo raw = null;
			Photo jpeg = null;

			IList<MergeRequest> merge_requests = new List<MergeRequest> ();

			for (int i = 0; i < photos.Length; i++) {
				Photo p = photos [i];

				ImageFile img = ImageFile.Create (p.DefaultVersion.Uri);
				if (!ImageFile.IsRaw (img) && !ImageFile.IsJpeg (img))
					continue;

				if (ImageFile.IsJpeg (img))
					jpeg = p;
				if (ImageFile.IsRaw (img))
					raw = p;

				if (raw != null && jpeg != null && SamePlaceAndName (raw, jpeg))
					merge_requests.Add (new MergeRequest (raw, jpeg));
			}

			if (merge_requests.Count == 0)
				return;

			foreach (MergeRequest mr in merge_requests)
				mr.Merge ();

			App.Instance.Organizer.UpdateQuery ();
		}

		private static bool SamePlaceAndName (Photo p1, Photo p2)
		{
			return DirectoryPath (p1) == DirectoryPath (p2) &&
				System.IO.Path.GetFileNameWithoutExtension (p1.Name) == System.IO.Path.GetFileNameWithoutExtension (p2.Name);
		}


		private static string DirectoryPath (Photo p)
		{
			System.Uri uri = p.VersionUri (Photo.OriginalVersionId);
			return uri.Scheme + "://" + uri.Host + System.IO.Path.GetDirectoryName (uri.AbsolutePath);
		}

		class MergeRequest
		{
			Photo raw;
			Photo jpeg;

			public MergeRequest (Photo raw, Photo jpeg)
			{
				this.raw = raw;
				this.jpeg = jpeg;
			}

			public void Merge ()
			{
				Console.WriteLine ("Merging {0} and {1}", raw.VersionUri (Photo.OriginalVersionId), jpeg.VersionUri (Photo.OriginalVersionId));
				foreach (uint version_id in jpeg.VersionIds) {
					string name = jpeg.GetVersion (version_id).Name;
					try {
						raw.DefaultVersionId = raw.CreateReparentedVersion (jpeg.GetVersion (version_id));
						if (version_id == Photo.OriginalVersionId)
							raw.RenameVersion (raw.DefaultVersionId, "Jpeg");
						else
							raw.RenameVersion (raw.DefaultVersionId, name);
					} catch (Exception e) {
						Console.WriteLine (e);
					}
				}
				raw.AddTag (jpeg.Tags);
				uint [] version_ids = jpeg.VersionIds;
				Array.Reverse (version_ids);
				foreach (uint version_id in version_ids) {
					try {
						jpeg.DeleteVersion (version_id, true);
					} catch (Exception e) {
						Console.WriteLine (e);
					}
				}
				raw.Changes.DataChanged = true;
				App.Instance.Database.Photos.Commit (raw);
				App.Instance.Database.Photos.Remove (jpeg);
			}
		}
	}
}
