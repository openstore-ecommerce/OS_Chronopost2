using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Compilation;
using System.Xml;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Localization;
using NBrightCore.common;
using NBrightDNN;
using Nevoweb.DNN.NBrightBuy.Components;
using Nevoweb.DNN.NBrightBuy.Components.Products;
using Nevoweb.DNN.NBrightBuy.Components.Interfaces;
using RazorEngine.Compilation.ImpromptuInterface.InvokeExt;
using Nevoweb.OS_Chronopost2.Components;

namespace Nevoweb.OS_Chronopost2
{
    public class AjaxProvider : AjaxInterface
    {
        public override string Ajaxkey { get; set; }

        public override string ProcessCommand(string paramCmd, HttpContext context, string editlang = "")
        {
            var ajaxInfo = NBrightBuyUtils.GetAjaxFields(context);
            var lang = NBrightBuyUtils.SetContextLangauge(ajaxInfo); // Ajax breaks context with DNN, so reset the context language to match the client.
            var objCtrl = new NBrightBuyController();

            var strOut = "OS_Chronopost Ajax Error";

            if (PluginUtils.CheckPluginSecurity(PortalSettings.Current.PortalId, "chronopost2"))
            {
                // NOTE: The paramCmd MUST start with the plugin ref. in lowercase. (links ajax provider to cmd)
                switch (paramCmd)
                {
                    case "chronopost2_save":
                        strOut = objCtrl.SavePluginSinglePageData(context);
                        break;
                }
            }
            switch (paramCmd)
            {
                case "chronopost2_select":
                    var currentcart = new CartData(PortalSettings.Current.PortalId);
                    var chronoData = new ChronopostLimpet(currentcart.PurchaseInfo);
                    chronoData.SelectedProductCode = ajaxInfo.GetXmlProperty("genxml/radiobuttonlist/productcode");
                    chronoData.Cart.Save();
                    strOut = NBrightBuyUtils.RazorTemplRender("carttemplate.cshtml", 0, "", chronoData, "/DesktopModules/NBright/OS_Chronopost2", "config", Utils.GetCurrentCulture(), StoreSettings.Current.Settings());
                    break;
            }

            return strOut;

        }
        public override void Validate()
        {
        }
        public void SetRelais()
        {

        }

    }
}
