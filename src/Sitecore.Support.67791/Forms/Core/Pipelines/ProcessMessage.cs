using System;
using System.Linq;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Extensions.StringExtensions;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Utility;
using Sitecore.Forms.Core.Data;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions;
using Sitecore.WFFM.Abstractions.Mail;
using Sitecore.WFFM.Abstractions.Shared;
using Sitecore.WFFM.Abstractions.Utils;
using Sitecore.WFFM.Abstractions.Dependencies;

namespace Sitecore.Support.Forms.Core.Pipelines
{
  public class ProcessMessage
  {
    private readonly string hrefReplacer;
    private readonly string shortHrefMediaReplacer;
    private readonly string shortHrefReplacer;
    private readonly string srcReplacer;

    public ProcessMessage() : this(DependenciesManager.WebUtil) {}

    public ProcessMessage(IWebUtil webUtil)
    {
      Assert.IsNotNull(webUtil, "webUtil");
      srcReplacer = string.Join(string.Empty, "src=\"", webUtil.GetServerUrl(), "/~");
      shortHrefReplacer = string.Join(string.Empty, "href=\"", webUtil.GetServerUrl(), "/");
      shortHrefMediaReplacer = string.Join(string.Empty, "href=\"", webUtil.GetServerUrl(), "/~/");
      hrefReplacer = shortHrefReplacer + "~";
    }

    public IFieldProvider FieldProvider { get; set; }

    public IItemRepository ItemRepository { get; set; }

    public void AddAttachments(ProcessMessageArgs args)
    {
      Assert.IsNotNull(ItemRepository, "ItemRepository");
      foreach (AdaptedControlResult result in args.Fields)
      {
        if (string.IsNullOrEmpty(result.Parameters) || !result.Parameters.StartsWith("medialink") || string.IsNullOrEmpty(result.Value)) continue;

        var uri = ItemUri.Parse(result.Value);

        if (uri != null)
        {          
          var innerItem = ItemRepository.GetItem(uri);
          if (innerItem == null)
          {            
            continue;
          }

          var item2 = new MediaItem(innerItem);         
          args.Attachments.Add(new Attachment(item2.GetMediaStream(), string.Join(".", item2.Name, item2.Extension), item2.MimeType));
        }
        else
        {
          var fileFromRequest = GetFileFromRequest(result.FieldID);
          if (fileFromRequest != null)
          {            
            args.Attachments.Add(new Attachment(fileFromRequest.InputStream, fileFromRequest.FileName, fileFromRequest.ContentType));
            SetCorrectValueForTheField(result, fileFromRequest);
          }             
        }
      }
    }

    private HttpPostedFile GetFileFromRequest(string fieldId)
    {
      if ((HttpContext.Current == null) || (HttpContext.Current.Request.Files.Count == 0))
      {        
        return null;
      }

      var files = HttpContext.Current.Request.Files;
      var allKeys = files.AllKeys;

      if (HttpContext.Current.Handler.GetType() == typeof(MvcHandler) || HttpContext.Current.Handler.GetType() == typeof(Sitecore.Mvc.Routing.RouteHttpHandler))
      {        
        var keys = HttpContext.Current.Request.Form.AllKeys;
        for (var i = 0; i < allKeys.Length; i++)
        {
          if (!keys.Any(key => key.Contains(fieldId) && allKeys[i].Contains(key.Replace(fieldId, ""))))
          {        
            continue;
          }

          var file = files[i];          
          file.InputStream.Position = 0L;                    
          return file;
        }
      }
      else
      {        
        var str = fieldId.Replace("-", string.Empty).TrimStart('{').TrimEnd('}');
        for (var k = 0; k < allKeys.Length; k++)
        {
          if (allKeys[k].Contains(str)) return files[k];
        }
      }
      return null;
    }

    private void SetCorrectValueForTheField(AdaptedControlResult field, HttpPostedFile postedFile)
    {
      string str2 = "/sitecore/media library"; ;      
      var item = new FieldItem(StaticSettings.ContextDatabase.GetItem(field.FieldID));
      var text = item["Parameters"];
      if (text != null)
      {        
        var index = text.IndexOf("<UploadTo>", StringComparison.InvariantCultureIgnoreCase);
        var length = text.IndexOf("</UploadTo>", StringComparison.InvariantCultureIgnoreCase) - index;

        if ((index < length) && (length > 0))
        {
          str2 = text.Mid(index, length);
        }
      }
           
      ReflectionUtils.SetProperty(field, "Value", string.Format("{0}/{1} (attached)", str2, postedFile.FileName), true);
    }
  }
}