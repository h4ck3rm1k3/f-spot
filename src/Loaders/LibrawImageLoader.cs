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

using Gdk;
using System;
using FSpot.Utils;
using FSpot.Loaders.Native;

namespace FSpot.Loaders {
	public class LibrawImageLoader : IImageLoader {

		public void Load (Uri uri)
		{

		}

		public event EventHandler<AreaPreparedEventArgs> AreaPrepared;
		public event EventHandler<AreaUpdatedEventArgs> AreaUpdated;
		public event EventHandler Completed;

		public bool Loading {
			get { throw new Exception ("Not implemented yet!"); }
		}

		public void Dispose ()
		{

		}

		public Pixbuf Pixbuf {
			get { throw new Exception ("Not implemented yet!"); }
		}

		public PixbufOrientation PixbufOrientation {
			get { throw new Exception ("Not implemented yet!"); }
		}
	}
}
