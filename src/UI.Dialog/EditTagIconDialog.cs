/*
 * FSpot.UI.Dialog.EditTagIconDialog.cs
 *
 * Author(s):
 * 	Larry Ewing <lewing@novell.com>
 * 	Stephane Delcroix <stephane@delcroix.org>
 *
 * Copyright (c) 2003-2009 Novell, Inc,
 * Copyright (c) 2007 Stephane Delcroix <stephane@delcroix.org>
 *
 * This is free software. See COPYING for details
 */

using System;
using Mono.Posix;
using Gtk;
using FSpot.Widgets;
using FSpot.Utils;

namespace FSpot.UI.Dialog
{
	public class EditTagIconDialog : BuilderDialog {
		Db db;
		FSpot.PhotoQuery query;
		PhotoImageView image_view;
		Gtk.IconView icon_view;
		ListStore icon_store;
		string icon_name = null;
		Gtk.FileChooserButton external_photo_chooser;


		[GtkBeans.Builder.Object] Gtk.Image preview_image;
		[GtkBeans.Builder.Object] ScrolledWindow photo_scrolled_window;
		[GtkBeans.Builder.Object] ScrolledWindow icon_scrolled_window;
		[GtkBeans.Builder.Object] Label photo_label;
		[GtkBeans.Builder.Object] Label from_photo_label;
		[GtkBeans.Builder.Object] Label from_external_photo_label;
		[GtkBeans.Builder.Object] private Label predefined_icon_label;
		[GtkBeans.Builder.Object] SpinButton photo_spin_button;
		[GtkBeans.Builder.Object] HBox external_photo_chooser_hbox;
		[GtkBeans.Builder.Object] Button noicon_button;
		
		public EditTagIconDialog (Db db, Tag t, Gtk.Window parent_window) : base ("EditTagIconDialog.ui", "edit_tag_icon_dialog")
		{
			this.db = db;
			TransientFor = parent_window;
			Title = String.Format (Catalog.GetString ("Edit Icon for Tag {0}"), t.Name);

			preview_pixbuf = t.Icon;
			if (preview_pixbuf != null && ColorManagement.IsEnabled) {
				preview_image.Pixbuf = preview_pixbuf.Copy ();
				ColorManagement.ApplyScreenProfile (preview_image.Pixbuf);
			} else
				preview_image.Pixbuf = preview_pixbuf;

			query = new FSpot.PhotoQuery (db.Photos);
			
			if (db.Tags.Hidden != null)
				query.Terms = FSpot.OrTerm.FromTags (new Tag []{ t, db.Tags.Hidden });
			else 
				query.Terms = new FSpot.Literal (t);

			image_view = new PhotoImageView (query);
			image_view.SelectionXyRatio = 1.0;
			image_view.SelectionChanged += HandleSelectionChanged;
			image_view.PhotoChanged += HandlePhotoChanged;

                        external_photo_chooser = new Gtk.FileChooserButton (Catalog.GetString ("Select Photo from file"),
                                                                 Gtk.FileChooserAction.Open);

			external_photo_chooser.Filter = new FileFilter();
			external_photo_chooser.Filter.AddPixbufFormats();
                        external_photo_chooser.LocalOnly = false;
                        external_photo_chooser_hbox.PackStart (external_photo_chooser);

			external_photo_chooser.SelectionChanged += HandleExternalFileSelectionChanged;

			photo_scrolled_window.Add (image_view);

			if (query.Count > 0) {
				photo_spin_button.Wrap = true;
				photo_spin_button.Adjustment.Lower = 1.0;
				photo_spin_button.Adjustment.Upper = (double) query.Count;
				photo_spin_button.Adjustment.StepIncrement = 1.0;
				photo_spin_button.ValueChanged += HandleSpinButtonChanged;
				
				image_view.Item.Index = 0;
			} else {
				from_photo_label.Markup = String.Format (Catalog.GetString (
					"\n<b>From Photo</b>\n" +
					" You can use one of your library photos as an icon for this tag.\n" +
					" However, first you must have at least one photo associated\n" +
					" with this tag. Please tag a photo as '{0}' and return here\n" +
					" to use it as an icon."), t.Name); 
				photo_scrolled_window.Visible = false;
				photo_label.Visible = false;
				photo_spin_button.Visible = false;
			}			

			icon_store = new ListStore (typeof (string), typeof (Gdk.Pixbuf));

			icon_view = new Gtk.IconView (icon_store); 
			icon_view.PixbufColumn = 1;
			icon_view.SelectionMode = SelectionMode.Single;
			icon_view.SelectionChanged += HandleIconSelectionChanged;

			icon_scrolled_window.Add (icon_view);

			icon_view.Show();

			image_view.Show ();

			FSpot.Delay fill_delay = new FSpot.Delay (FillIconView);
			fill_delay.Start ();
		}

		public FSpot.BrowsablePointer Item {
			get { return image_view.Item; }
		}

		Gdk.Pixbuf preview_pixbuf;
		public Gdk.Pixbuf PreviewPixbuf {
			get { return preview_pixbuf; }
			set {
				icon_name = null;
				preview_pixbuf = value;
				if (value != null && ColorManagement.IsEnabled) {
					preview_image.Pixbuf = value.Copy ();
					ColorManagement.ApplyScreenProfile (preview_image.Pixbuf);
				} else
					preview_image.Pixbuf = value;

			}

		}

		public string ThemeIconName {
			get { return icon_name; }
			set {
				icon_name = value;	
				preview_image.Pixbuf = GtkUtil.TryLoadIcon (FSpot.Global.IconTheme, value, 48, (IconLookupFlags) 0);
			}
			
		}

		
		void HandleSpinButtonChanged (object sender, EventArgs args)
		{
			int value = photo_spin_button.ValueAsInt - 1;
			
			image_view.Item.Index = value;
		}

		void HandleExternalFileSelectionChanged (object sender, EventArgs args)
		{	//Note: The filter on the FileChooserButton's dialog means that we will have a Pixbuf compatible uri here
			CreateTagIconFromExternalPhoto ();
		}

		void CreateTagIconFromExternalPhoto ()
		{
			try {
				using (FSpot.ImageFile img = FSpot.ImageFile.Create(new Uri(external_photo_chooser.Uri))) {
					using (Gdk.Pixbuf external_image = img.Load ()) {
						PreviewPixbuf = PixbufUtils.TagIconFromPixbuf (external_image);
					}
				}
			} catch (Exception) {
				string caption = Catalog.GetString ("Unable to load image");
				string message = String.Format (Catalog.GetString ("Unable to load \"{0}\" as icon for the tag"), 
									external_photo_chooser.Uri.ToString ());
				HigMessageDialog md = new HigMessageDialog (this, 
									    DialogFlags.DestroyWithParent,
									    MessageType.Error,
									    ButtonsType.Close,
									    caption, 
									    message);
				md.Run();
				md.Destroy();
			}
		}

		void HandleSelectionChanged (object sender, EventArgs e)
		{
		       	int x = image_view.Selection.X;
		       	int y = image_view.Selection.Y;
			int width = image_view.Selection.Width;
			int height = image_view.Selection.Height;
		       
//			if (width > 0 && height > 0) 
//				icon_view.Selection.Clear ();
				
			if (image_view.Pixbuf != null) {
				if (width > 0 && height > 0) {
					using (var tmp = new Gdk.Pixbuf (image_view.Pixbuf, x, y, width, height)) {	
						PreviewPixbuf = PixbufUtils.TagIconFromPixbuf (tmp);
					}
				} else {
					PreviewPixbuf = PixbufUtils.TagIconFromPixbuf (image_view.Pixbuf);
				}
			}
		}

		public void HandlePhotoChanged (object sender, EventArgs e)
		{
			int item = image_view.Item.Index;
			photo_label.Text = String.Format (Catalog.GetString ("Photo {0} of {1}"), 
							  item + 1, query.Count);

			photo_spin_button.Value = item + 1;
		}

		public void HandleIconSelectionChanged (object o, EventArgs args)
		{
			if (icon_view.SelectedItems.Length == 0)
				return;

			TreeIter iter;
			icon_store.GetIter (out iter, icon_view.SelectedItems [0]); 
			ThemeIconName = (string) icon_store.GetValue (iter, 0);
		}

		public bool FillIconView ()
		{
			icon_store.Clear ();
			string [] icon_list = FSpot.Global.IconTheme.ListIcons ("Emblems");
			foreach (string item_name in icon_list)
				icon_store.AppendValues (item_name, GtkUtil.TryLoadIcon (FSpot.Global.IconTheme, item_name, 32, (IconLookupFlags) 0));
			return false;
		}
	}
}