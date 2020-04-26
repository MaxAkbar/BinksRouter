﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Windows;
using BinksRouter.Annotations;
using BinksRouter.Extensions;
using BinksRouter.Network.Entities;

namespace BinksRouter.Network
{
    public class RoutingTable : ObservableCollection<Route>
    {

        private readonly object _lock = new object();

        [CanBeNull]
        public Route Resolve(IPAddress ipAddress)
        {
            Route bestRoute = null;

            lock (_lock)
            {
                foreach (var route in this.Where(item => ipAddress.IsInSameSubnet(item.NetworkAddress, item.NetworkMask)))
                {
                    if (bestRoute == null || bestRoute.NetworkMask.ToInt() > route.NetworkMask.ToInt())
                    {
                        bestRoute = route;
                    }
                }
            }

            if (bestRoute == null)
            {
                return null;
            }

            return bestRoute.Interface != null ? bestRoute : Resolve(bestRoute.NextHop);
        }

        public bool Learn(RipPacket.RipRecord ripRecord, Interface origin)
        {
            var existingRoute = this.FirstOrDefault(item => item.NetworkAddress.Equals(ripRecord.IpAddress) && item.NetworkMask.Equals(ripRecord.Mask));

            if (existingRoute != null && existingRoute.Metric > ripRecord.Metric)
            {
                Remove(existingRoute);
                Add(new Route(ripRecord, origin));
                return true;
            } 
            else if (existingRoute == null)
            {
                Add(new Route(ripRecord, origin));
                return true;
            }

            return false;
        }

        public new void Add(Route item)
        {
            lock (_lock)
            {
                if (!Contains(item))
                {
                    Application.Current.Dispatcher.Invoke((Action)delegate
                    {
                        item.PropertyChanged += RouteChanged;
                        base.Add(item);
                    });
                }
            }
        }

        private void RouteChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is Route route)
            {
                if (route.Status.Equals(Route.RouteStatus.Flush))
                {
                    Remove(route);
                }
            }
        }

        public new bool Remove(Route item)
        {
            lock (_lock)
            {
                var result = false;
                Application.Current.Dispatcher.Invoke((Action)delegate
                {
                    result = base.Remove(item);
                });
                return result;
            }
        }

        public RipPacket ToPacket()
        {
            var packet = new RipPacket(RipPacket.RipCommand.Response);

            foreach (var route in this.Where(item => (item.RipEnabled)))
            {
                packet.Records.Add(new RipPacket.RipRecord(route));
            }

            return packet;
        }
    }
}
