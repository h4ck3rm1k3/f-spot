//
// Fspot.Loaders.LibrawImageLoader.cs
//
// Copyright (c) 2009 Novell, Inc.
//
// Author(s)
//	Ruben Vermeersch  <ruben@savanne.be>
//
// This is free software. See COPYING for details
//

using FSpot.Loaders.Native;
using FSpot.Platform;
using FSpot.Utils;
using Gdk;
using System;
using System.Threading;

namespace FSpot.Loaders {
	public class LibrawImageLoader : IImageLoader
	{
		Uri uri;
		object sync_handle = new object ();
		bool is_disposed = false;
		Rectangle damage;

		public ImageLoaderItem ItemsRequested { get; private set; }
		public ImageLoaderItem ItemsCompleted { get; private set; }

		Pixbuf thumbnail;
		public Pixbuf Thumbnail {
			get { return PixbufUtils.ShallowCopy (thumbnail); }
			private set { thumbnail = value; }
		}
		public PixbufOrientation ThumbnailOrientation { get; private set; }

		Pixbuf large;
		public Pixbuf Large {
			get { return PixbufUtils.ShallowCopy (large); }
		}
		public PixbufOrientation LargeOrientation { get; private set; }

		Pixbuf full;
		public Pixbuf Full {
			get { return PixbufUtils.ShallowCopy (full); }
		}
		public PixbufOrientation FullOrientation { get; private set; }

		public event EventHandler<AreaPreparedEventArgs> AreaPrepared;
		public event EventHandler<AreaUpdatedEventArgs> AreaUpdated;
		public event EventHandler<ItemsCompletedEventArgs> Completed;

		public bool Loading { get; private set; }

		NativeLibrawLoader loader;

#region public api
		public LibrawImageLoader (Uri uri) : base ()
		{
			this.uri = uri;
			Loading = false;

			ItemsRequested = ImageLoaderItem.None;
			ItemsCompleted = ImageLoaderItem.None;

			loader = new NativeLibrawLoader (uri.AbsolutePath);
		}

		public ImageLoaderItem Load (ImageLoaderItem items, bool async)
		{
			if (is_disposed)
				return ImageLoaderItem.None;

			ItemsRequested |= items;

			StartLoading ();

			if (!async)
				WaitForCompletion (items);

			return ItemsCompleted & items;
		}

		public void Dispose ()
		{
			is_disposed = true;
			if (loader != null) {
				loader.Aborted = true;
				loader = null;
			}
			if (thumbnail != null)
				thumbnail.Dispose ();
			if (large != null)
				large.Dispose ();
			if (full != null)
				full.Dispose ();
		}
#endregion

#region private stuffs
		void StartLoading ()
		{
			lock (sync_handle) {
				if (Loading)
					return;
				Loading = true;
			}

			// Load thumbnail immediately, if required
			if (!ItemsCompleted.Contains (ImageLoaderItem.Thumbnail) &&
				 ItemsRequested.Contains (ImageLoaderItem.Thumbnail)) {
				LoadThumbnail ();
			}

			ThreadPool.QueueUserWorkItem (delegate {
					try {
						DoLoad ();
					} catch (Exception e) {
						Log.Debug (e.ToString ());
						Log.Debug ("Requested: {0}, Done: {1}", ItemsRequested, ItemsCompleted);
						Gtk.Application.Invoke (delegate { throw e; });
					}
				});
		}

		void DoLoad ()
		{
			while (!is_disposed && !ItemsCompleted.Contains (ItemsRequested)) {
				if (ItemsRequested.Contains (ImageLoaderItem.Thumbnail))
					LoadThumbnail ();

				if (ItemsRequested.Contains (ImageLoaderItem.Large))
					LoadLarge ();

				if (ItemsRequested.Contains (ImageLoaderItem.Full))
					LoadFull ();
			}

			lock (sync_handle) {
				Loading = false;
			}
		}

		void LoadThumbnail ()
		{
			if (is_disposed)
				return;

			// Check if the thumbnail exists, if not: try to create it from the
			// Large image. Will request Large if it is not present and wait
			// for the next call to generate it (see the loop in DoLoad).
			if (!ThumbnailFactory.ThumbnailExists (uri)) {
				if (ItemsCompleted.Contains (ImageLoaderItem.Large)) {
					ThumbnailFactory.SaveThumbnail (Large, uri);
				} else {
					ItemsRequested |= ImageLoaderItem.Large;
					return;
				}
			}

			Thumbnail = ThumbnailFactory.LoadThumbnail (uri);
			ThumbnailOrientation = PixbufOrientation.TopLeft;
			if (Thumbnail == null)
				throw new Exception ("Null thumbnail returned");

			SignalAreaPrepared (ImageLoaderItem.Thumbnail);
			SignalAreaUpdated (ImageLoaderItem.Thumbnail, new Rectangle (0, 0, Thumbnail.Width, Thumbnail.Height));
			SignalItemCompleted (ImageLoaderItem.Thumbnail);
		}


		void LoadLarge ()
		{
			if (is_disposed)
				return;

			int orientation;
			large = loader.LoadEmbedded (out orientation);

			switch (orientation) {
				case 0:
					LargeOrientation = PixbufOrientation.TopLeft;
					break;

				case 3:
					LargeOrientation = PixbufOrientation.BottomRight;
					break;

				case 5:
					LargeOrientation = PixbufOrientation.LeftBottom;
					break;

				case 6:
					LargeOrientation = PixbufOrientation.RightBottom;
					break;

				default:
					throw new Exception ("Unexpected orientation returned!");
			}

			SignalAreaPrepared (ImageLoaderItem.Large);
			SignalAreaUpdated (ImageLoaderItem.Large, new Rectangle (0, 0, large.Width, large.Height));
			SignalItemCompleted (ImageLoaderItem.Large);
		}

		void LoadFull ()
		{
			if (is_disposed)
				return;

			loader.ProgressUpdated += delegate (object o, ProgressUpdatedArgs args) {
				Log.Debug ("Loading RAW: {0}/{1}", args.Done, args.Total);
			};
			full = loader.LoadFull ();
			FullOrientation = PixbufOrientation.TopLeft;
			if (full == null) {
				return;
			}

			SignalAreaPrepared (ImageLoaderItem.Full);
			SignalAreaUpdated (ImageLoaderItem.Full, new Rectangle (0, 0, full.Width, full.Height));
			SignalItemCompleted (ImageLoaderItem.Full);
		}

		void WaitForCompletion (ImageLoaderItem items)
		{
			while (!ItemsCompleted.Contains(items)) {
				Log.Debug ("Waiting for completion of {0} (done: {1})", ItemsRequested, ItemsCompleted);
				Monitor.Enter (sync_handle);
				Monitor.Wait (sync_handle);
				Monitor.Exit (sync_handle);
				Log.Debug ("Woke up after waiting for {0} (done: {1})", ItemsRequested, ItemsCompleted);
			}
		}

		void SignalAreaPrepared (ImageLoaderItem item) {
			damage = Rectangle.Zero;
			EventHandler<AreaPreparedEventArgs> eh = AreaPrepared;
			if (eh != null)
				GLib.Idle.Add (delegate {
					eh (this, new AreaPreparedEventArgs (item));
					return false;
				});
		}

		void SignalAreaUpdated (ImageLoaderItem item, Rectangle area) {
			EventHandler<AreaUpdatedEventArgs> eh = AreaUpdated;
			if (eh == null)
				return;

			lock (sync_handle) {
				if (damage == Rectangle.Zero) {
					damage = area;
					GLib.Idle.Add (delegate {
						Rectangle to_signal;
						lock (sync_handle) {
							to_signal = damage;
							damage = Rectangle.Zero;
						}
						eh (this, new AreaUpdatedEventArgs (item, to_signal));
						return false;
					});
				} else {
					damage = damage.Union (area);
				}
			}
		}

		void SignalItemCompleted (ImageLoaderItem item)
		{
			ItemsCompleted |= item;
			Log.Debug ("Notifying completion of {0} (done: {1}, requested: {2})", item, ItemsCompleted, ItemsRequested);

			Monitor.Enter (sync_handle);
			Monitor.PulseAll (sync_handle);
			Monitor.Exit (sync_handle);

			Log.Debug ("Signalled!");

			EventHandler<ItemsCompletedEventArgs> eh = Completed;
			if (eh != null)
				GLib.Idle.Add (delegate {
					eh (this, new ItemsCompletedEventArgs (item));
					return false;
				});
		}
#endregion
	}
}
