
namespace FSpot {
	public class PhotoImageView : ImageView {
		public PhotoImageView (PhotoQuery query)
		{
			this.query = query;
			loader = new FSpot.AsyncPixbufLoader ();
			//scroll_delay = new Delay (new GLib.IdleHandler (IdleUpdateScrollbars));
			this.SizeAllocated += HandleSizeAllocated;
			this.KeyPressEvent += HandeKeyPressEvent;
		}
		
		private int current_photo = -1;
		public int CurrentPhoto {
			get {
				return current_photo;
			}
			set {
				if (current_photo == value && this.Pixbuf != null){
					return;
				} else {
					current_photo = value;
					this.PhotoChanged ();
				}
			}
		}

		public Photo Photo {
			get {
				if (CurrentPhotoValid ()) 
					return this.Query.Photos [CurrentPhoto];
				else 
					return null;
			}
		}

		private PhotoQuery query;
		public PhotoQuery Query {
			get {
				return query;
			}
			set {
				if (query != null) {
					//query.Reload -= HandleQueryReload;
					//query.ItemChanged -= HandleQueryItemChanged;
				}

				query = value;
				//query.Reload += HandleQueryItemReload;
				//query.ItemChanged += HandleQueryItemChanged;
				
				CurrentPhoto = 0;
			}
		}

		public bool CurrentPhotoValid ()
		{
			if (query == null ||
			    query.Photos.Length == 0 ||
			    CurrentPhoto >= Query.Photos.Length ||
			    CurrentPhoto < 0) {
				System.Console.WriteLine ("Invalid CurrentPhoto");
				return false;
			}

			return true;
		}

		// Display.
		private void HandlePixbufAreaUpdated (object sender, Gdk.AreaUpdatedArgs args)
		{
			Gdk.Rectangle area = new Gdk.Rectangle (args.X, args.Y, args.Width, args.Height);
			area = this.ImageCoordsToWindow (area);

			this.QueueDrawArea (area.X, area.Y, area.Width, area.Height);
		}
	
		private bool fit = true;
		public bool Fit {
			get {
				return fit;
			}
			set {
				fit = value;
				if (fit)
					ZoomFit ();
			}
		}


		public double Zoom {
			get {
				double x, y;
				this.GetZoom (out x, out y);
				return x;
			}
			
			set {
				this.Fit = false;
				this.SetZoom (value, value);
			}
		}
		
		private void HandleSizeAllocated (object sender, Gtk.SizeAllocatedArgs args)
		{
			if (fit)
				ZoomFit ();
		}	

		bool load_async = true;
		FSpot.AsyncPixbufLoader loader;
		FSpot.AsyncPixbufLoader next_loader;

		private void PhotoChanged () 
		{
			if (!CurrentPhotoValid ())
				return;

			Gdk.Pixbuf old = this.Pixbuf;
			Gdk.Pixbuf current = null;
			
			if (load_async) {
				current = loader.Load (Photo.DefaultVersionPath);
				loader.Loader.AreaUpdated += HandlePixbufAreaUpdated;
			} else
				current = FSpot.PhotoLoader.Load (Query, current_photo);
			
			this.Pixbuf = current;
			
			if (old != null)
				old.Dispose ();

			this.UnsetSelection ();
			this.ZoomFit ();
		}

		private Delay scroll_delay;
		private bool IdleUpdateScrollbars ()
		{
			(this.Parent as Gtk.ScrolledWindow).SetPolicy (Gtk.PolicyType.Automatic, 
								       Gtk.PolicyType.Automatic);
			return false;
 		}

		private void ZoomFit ()
		{
			Gdk.Pixbuf pixbuf = this.Pixbuf;
			
			System.Console.WriteLine ("ZoomFit");

			if (pixbuf == null) {
				System.Console.WriteLine ("pixbuf == null");
				return;
			}
			int available_width = this.Allocation.Width;
			int available_height = this.Allocation.Height;

		
			double zoom_to_fit = ZoomUtils.FitToScale ((uint) available_width, (uint) available_height,
								   (uint) pixbuf.Width, (uint) pixbuf.Height, false);
			
			double image_zoom = zoom_to_fit;
			System.Console.WriteLine ("Zoom = {0}, {1}, {2}", image_zoom, 
						  available_width, 
						  available_height);
			
			//if (System.Math.Abs (Zoom) < double.Epsilon)
				((Gtk.ScrolledWindow) this.Parent).SetPolicy (Gtk.PolicyType.Never, Gtk.PolicyType.Never);

			this.SetZoom (image_zoom, image_zoom);
			
			((Gtk.ScrolledWindow) this.Parent).SetPolicy (Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
		}

		public void Next () {
			if (CurrentPhoto + 1 < query.Photos.Length)
				CurrentPhoto++;
			else
				CurrentPhoto = 0;
		}
		
		public void Prev () 
		{
			if (CurrentPhoto > 0)
				CurrentPhoto --;
			else
				CurrentPhoto = query.Photos.Length - 1;
		}

		protected override void OnDestroyed ()
		{
			System.Console.WriteLine ("I'm feeling better");
			base.OnDestroyed ();
		}

		[GLib.ConnectBefore]
		private void HandeKeyPressEvent (object sender, Gtk.KeyPressEventArgs args)
		{
			// FIXME I really need to figure out why overriding is not working
			// for any of the default handlers.

			switch (args.Event.Key) {
			case Gdk.Key.Page_Up:
			case Gdk.Key.KP_Page_Up:
				this.Prev ();
				break;
			case Gdk.Key.Page_Down:
			case Gdk.Key.KP_Page_Down:
				this.Next ();
				break;
			case Gdk.Key.Key_0:
				this.Fit = true;
				break;
			case Gdk.Key.Key_1:
				this.Zoom =  1.0;
				break;
			case Gdk.Key.Key_2:
				this.Zoom = 2.0;
				break;
			default:
				args.RetVal = false;
				return;
			}
			args.RetVal = true;
			return;
		}

		protected override bool OnDestroyEvent (Gdk.Event evnt)
		{
			System.Console.WriteLine ("I'm feeling better");
			return base.OnDestroyEvent (evnt);
		}
	}
}
