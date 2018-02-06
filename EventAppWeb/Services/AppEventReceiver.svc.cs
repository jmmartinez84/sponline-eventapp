using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint.Client;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.SharePoint.Client.EventReceivers;

namespace EventAppWeb.Services
{
    public class AppEventReceiver : IRemoteEventService
    {
        private const string ReceiverName = "ItemAddedEvent";
        private const string ListName = "Announcements";
        /// <summary>
        /// Handles app events that occur after the app is installed or upgraded, or when app is being uninstalled.
        /// </summary>
        /// <param name="properties">Holds information about the app event.</param>
        /// <returns>Holds information returned from the app event.</returns>
        public SPRemoteEventResult ProcessEvent(SPRemoteEventProperties properties)
        {
            SPRemoteEventResult result = new SPRemoteEventResult();

            switch (properties.EventType)
            {
                case SPRemoteEventType.AppInstalled:
                    HandleAppInstalled(properties);
                    break;
                case SPRemoteEventType.AppUninstalling:
                    HandleAppUninstalling(properties);
                    break;
                case SPRemoteEventType.ItemAdded:
                    HandleItemAdded(properties);
                    break;
            }


            return result;
        }
        private void HandleAppInstalled(SPRemoteEventProperties properties)
        {
            using (ClientContext clientContext = TokenHelper.CreateAppEventClientContext(properties, false))
            {
                if (clientContext != null)
                {
                    List myList = clientContext.Web.Lists.GetByTitle(ListName);
                    clientContext.Load(myList, p => p.EventReceivers);
                    clientContext.ExecuteQuery();
                    bool rerExists = false;
                    foreach (var rer in myList.EventReceivers)
                    {
                        if (rer.ReceiverName == ReceiverName)
                        {
                            rerExists = true;
                            System.Diagnostics.Trace.WriteLine("Found existing ItemAdded receiver at " + rer.ReceiverUrl);
                        }
                    }
                    if (!rerExists)
                    {
                        EventReceiverDefinitionCreationInformation receiver = new EventReceiverDefinitionCreationInformation();
                        receiver.EventType = EventReceiverType.ItemAdded;
                        //Get WCF URL where this message was handled
                        OperationContext op = OperationContext.Current;
                        Message msg = op.RequestContext.RequestMessage;
                        receiver.ReceiverUrl = msg.Headers.To.ToString();
                        receiver.ReceiverName = ReceiverName;
                        receiver.Synchronization = EventReceiverSynchronization.Synchronous;
                        myList.EventReceivers.Add(receiver);
                        clientContext.ExecuteQuery();
                        System.Diagnostics.Trace.WriteLine("Added ItemAdded receiver at "+msg.Headers.To.ToString());
                    }
                }
            }
        }
        private void HandleAppUninstalling(SPRemoteEventProperties properties)
        {
            using (ClientContext clientContext = TokenHelper.CreateAppEventClientContext(properties, false))
            {
                if (clientContext != null)
                {
                    List myList = clientContext.Web.Lists.GetByTitle(ListName);
                    clientContext.Load(myList, p => p.EventReceivers);
                    clientContext.ExecuteQuery();
                    var rer = myList.EventReceivers.Where(e => e.ReceiverName == ReceiverName).FirstOrDefault();
                    try
                    {
                        System.Diagnostics.Trace.WriteLine("Removing ItemAdded receiver at " + rer.ReceiverUrl);
                        //This will fail when deploying via F5, but works
                        //when deployed to production
                        rer.DeleteObject();
                        clientContext.ExecuteQuery();
                    }
                    catch (Exception oops)
                    {
                        System.Diagnostics.Trace.WriteLine(oops.Message);
                    }
                }
            }
        }
        private void HandleItemAdded(SPRemoteEventProperties properties)
        {
            using (ClientContext clientContext = TokenHelper.CreateRemoteEventReceiverClientContext(properties))
            {
                if (clientContext != null)
                {
                    try
                    {
                        List photos = clientContext.Web.Lists.GetById(properties.ItemEventProperties.ListId);
                        ListItem item = photos.GetItemById(properties.ItemEventProperties.ListItemId);
                        clientContext.Load(item);
                        clientContext.ExecuteQuery();
                        item["Title"] += "\nUpdated by RER " + System.DateTime.Now.ToLongTimeString();
                        item.Update();
                        clientContext.ExecuteQuery();
                    }
                    catch (Exception oops)
                    {
                        System.Diagnostics.Trace.WriteLine(oops.Message);
                    }
                }
            }
        }
        /// <summary>
        /// This method is a required placeholder, but is not used by app events.
        /// </summary>
        /// <param name="properties">Unused.</param>
        public void ProcessOneWayEvent(SPRemoteEventProperties properties)
        {
            throw new NotImplementedException();
        }

    }
}
