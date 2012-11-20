using System;
using System.Linq;
using System.Web.UI.WebControls;
using N2.Definitions;
using N2.Persistence;
using N2.Linq;
using N2.Management.Content.LinkTracker;
using N2.Web;
using N2.Edit.FileSystem.Items;
using N2.Web.Drawing;

namespace N2.Edit.LinkTracker
{
	public partial class UpdateReferences : N2.Edit.Web.EditPage
	{
		private Tracker tracker;
		private string previousName;
		private string previousUrl;
		private bool isRenamingDirectory;
		private ContentItem previousParent;

		protected override void OnInit(EventArgs e)
		{
			tracker = Engine.Resolve<Tracker>();

			base.OnInit(e);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);

			Refresh(Selection.SelectedItem, ToolbarArea.Navigation);

			Title = "Update links leading to " + Selection.SelectedItem.Title;

			previousParent = Engine.Resolve<Navigator>().Navigate(Request["previousParent"]);
			previousName = Request["previousName"];

			if (Selection.SelectedItem is IFileSystemNode)
			{
				previousUrl = Request["previousParent"] + Request["previousName"];
			}

			isRenamingDirectory = Selection.SelectedItem is IFileSystemDirectory;

			if (!IsPostBack)
			{
				var item = Selection.SelectedItem;
				var referrers = Selection.SelectedItem.ID == 0
					? tracker.FindReferrers(previousParent.Url.ToUrl().AppendSegment(previousName).SetExtension(item.Extension)).ToList()
					: tracker.FindReferrers(Selection.SelectedItem).ToList();
				bool showReferences = referrers.Count > 0;
				if (showReferences)
				{
					rptReferencingItems.DataSource = referrers;
					DataBind();
				}
				else
					fsReferences.Visible = false;

				bool showChildren = Selection.SelectedItem.Children.Count > 0;
				if (showChildren)
				{
					targetsToUpdate.CurrentItem = Selection.SelectedItem;
					targetsToUpdate.DataBind();
				}
				else
					fsChildren.Visible = false;

				chkPermanentRedirect.Visible = previousParent != null 
					&& Selection.SelectedItem.ID != 0
					&& Engine.Resolve<Configuration.EditSection>().LinkTracker.PermanentRedirectEnabled;

				if (!showReferences && !showChildren && previousParent == null)
				{
					Refresh(Selection.SelectedItem, ToolbarArea.Both);
				}
			}
		}

		protected void OnUpdateCommand(object sender, CommandEventArgs args)
		{
			if (chkPermanentRedirect.Checked && previousParent != null)
			{
				var redirect = Engine.Resolve<ContentActivator>().CreateInstance<PermanentRedirect>(previousParent);
				redirect.Title = previousName + GetLocalResourceString("PermanentRedirect", " (permanent redirect)");
				redirect.Name = previousName;
				redirect.RedirectUrl = Selection.SelectedItem.Url;
				redirect.RedirectTo = Selection.SelectedItem;
				redirect.AddTo(previousParent);
				
				Engine.Persister.Save(redirect);
			}

			tracker.UpdateReferencesTo(
				Selection.SelectedItem,
				previousUrl,
				isRenamingDirectory
				);

			if (Selection.SelectedItem is IFileSystemFile)
			{
				this.Engine.Resolve<ImageSizeCache>().ImageSizes
					// Original size is returned as empty string
					.Where(size => !string.IsNullOrEmpty(size)).ToList()
					.ForEach(size =>
					{
						string previousSizeUrl = ImagesUtility.GetResizedPath(previousUrl, size);
						string filename = System.IO.Path.GetFileName(previousSizeUrl.Replace("/", "\\"));
						tracker.UpdateReferencesTo(
							Selection.SelectedItem.Children.First(c => c.Name == filename),
							previousSizeUrl,
							isRenamingDirectory: false
						);
					});
			}

			if (chkChildren.Checked)
			{
				mvPhase.ActiveViewIndex = 1;
				rptDescendants.DataSource = Content.Search.Find.Where.AncestralTrail.Like(Selection.SelectedItem.GetTrail() + "%").Select()
					.Where(Content.Is.Accessible());
				rptDescendants.DataBind();
			}
			else
			{
				Refresh(Selection.SelectedItem, ToolbarArea.Both);
			}
		}
	}
}