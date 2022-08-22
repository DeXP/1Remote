﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Service;
using _1RM.View;
using Shawn.Utils;
using Stylet;

namespace _1RM.Model
{
    public class GlobalData : NotifyPropertyChangedBase
    {
        public GlobalData(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
            ConnectTimeRecorder.Init(AppPathHelper.Instance.ConnectTimeRecord);
            ReloadServerList();
        }

        private IDataService? _dataService;
        private readonly ConfigurationService _configurationService;

        public void SetDbOperator(IDataService dataService)
        {
            _dataService = dataService;
        }


        public bool TagListDoInvokeSelectedTabName = true;
        private ObservableCollection<Tag> _tagList = new ObservableCollection<Tag>();
        public ObservableCollection<Tag> TagList
        {
            get => _tagList;
            private set => SetAndNotifyIfChanged(ref _tagList, value);
        }



        #region Server Data

        public Action? VmItemListDataChanged;

        public List<ProtocolBaseViewModel> VmItemList { get; set; } = new List<ProtocolBaseViewModel>();


        private void ReadTagsFromServers()
        {
            var pinnedTags = _configurationService.PinnedTags;

            // get distinct tag from servers
            var tags = new List<Tag>();
            foreach (var tagNames in VmItemList.Select(x => x.Server.Tags))
            {
                foreach (var tagName in tagNames)
                {
                    if (tags.All(x => x.Name != tagName))
                        tags.Add(new Tag(tagName, pinnedTags.Contains(tagName), SaveOnPinnedChanged) { ItemsCount = 1 });
                    else
                        tags.First(x => x.Name == tagName).ItemsCount++;
                }
            }

            TagListDoInvokeSelectedTabName = false;
            TagList = new ObservableCollection<Tag>(tags.OrderBy(x => x.Name));
            TagListDoInvokeSelectedTabName = true;
        }

        private void SaveOnPinnedChanged()
        {
            _configurationService.PinnedTags = TagList.Where(x => x.IsPinned).Select(x => x.Name).ToList();
            _configurationService.Save();
        }

        public void ReloadServerList()
        {
            if (_dataService == null)
            {
                return;
            }
            // read from db
            var tmp = new List<ProtocolBaseViewModel>();
            var dbServers = _dataService.Database_GetServers();
            foreach (var server in dbServers)
            {
                var serverAbstract = server;
                try
                {
                    Execute.OnUIThread(() =>
                    {
                        _dataService.DecryptToRamLevel(ref serverAbstract);
                        var vm = new ProtocolBaseViewModel(serverAbstract)
                        {
                            LastConnectTime = ConnectTimeRecorder.Get(serverAbstract.Id)
                        };
                        tmp.Add(vm);
                    });
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Info(e);
                }
            }

            VmItemList = tmp;
            ConnectTimeRecorder.Cleanup(VmItemList.Select(x => x.Id));
            ReadTagsFromServers();
            VmItemListDataChanged?.Invoke();
        }

        public void UnselectAllServers()
        {
            foreach (var item in VmItemList)
            {
                item.IsSelected = false;
            }
        }

        public void AddServer(ProtocolBase protocolServer, bool doInvoke = true)
        {
            if (_dataService == null) return;
            _dataService.Database_InsertServer(protocolServer);
            if (doInvoke)
            {
                ReloadServerList();
            }
        }

        public void AddServer(IEnumerable<ProtocolBase> protocolServers, bool doInvoke = true)
        {
            if (_dataService == null)
            {
                return;
            }

            _dataService.Database_InsertServer(protocolServers);
            if (doInvoke)
            {
                ReloadServerList();
            }
        }

        public void UpdateServer(ProtocolBase protocolServer, bool doInvoke = true)
        {
            if (_dataService == null) return;
            Debug.Assert(string.IsNullOrEmpty(protocolServer.Id) == false);
            UnselectAllServers();
            _dataService.Database_UpdateServer(protocolServer);
            int i = VmItemList.Count;
            if (VmItemList.Any(x => x.Id == protocolServer.Id))
            {
                var old = VmItemList.First(x => x.Id == protocolServer.Id);
                if (old.Server != protocolServer)
                {
                    i = VmItemList.IndexOf(old);
                    VmItemList.Remove(old);
                    VmItemList.Insert(i, new ProtocolBaseViewModel(protocolServer));
                }
            }

            if (doInvoke)
            {
                ReadTagsFromServers();
                VmItemListDataChanged?.Invoke();
            }
        }

        public void UpdateServer(IEnumerable<ProtocolBase> protocolServers, bool doInvoke = true)
        {
            if (_dataService == null)
            {
                return;
            }

            if (_dataService.Database_UpdateServer(protocolServers))
                if (doInvoke)
                    ReloadServerList();
        }

        public void DeleteServer(string id, bool doInvoke = true)
        {
            if (_dataService == null)
            {
                return;
            }
            if (_dataService.Database_DeleteServer(id))
            {
                if (doInvoke)
                    ReloadServerList();
            }
        }

        public void DeleteServer(IEnumerable<string> ids, bool doInvoke = true)
        {
            if (_dataService == null)
            {
                return;
            }

            if (_dataService.Database_DeleteServer(ids))
            {
                if (doInvoke)
                    ReloadServerList();
            }
        }

        #endregion Server Data
    }
}