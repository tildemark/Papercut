﻿// Papercut
// 
// Copyright © 2008 - 2012 Ken Robertson
// Copyright © 2013 - 2014 Jaben Cargman
//  
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//  
// http://www.apache.org/licenses/LICENSE-2.0
//  
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Papercut.ViewModels
{
    using System;
    using System.Diagnostics;
    using System.Reactive.Concurrency;
    using System.Reactive.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    using Caliburn.Micro;

    using MahApps.Metro.Controls;
    using MahApps.Metro.Controls.Dialogs;

    using Microsoft.Build.Utilities;

    using Papercut.Core.Events;
    using Papercut.Core.Message;
    using Papercut.Core.Network;
    using Papercut.Events;
    using Papercut.Helpers;
    using Papercut.Properties;

    public class MainViewModel : Screen,
        IHandle<SmtpServerBindFailedEvent>,
        IHandle<ShowMessageEvent>,
        IHandle<ShowMainWindowEvent>,
        IHandle<ShowOptionWindowEvent>
    {
        const string WindowTitleDefault = "Papercut";

        readonly IPublishEvent _publishEvent;

        readonly MessageRepository _messageRepository;

        readonly IViewModelWindowManager _viewModelWindowManager;

        bool _isDeactivated;

        MetroWindow _window;

        string _windowTitle = WindowTitleDefault;

        public MainViewModel(
            IViewModelWindowManager viewModelWindowManager,
            IPublishEvent publishEvent,
            MessageRepository messageRepository,
            Func<MessageListViewModel> messageListViewModelFactory,
            Func<MessageDetailViewModel> messageDetailViewModelFactory)
        {
            _viewModelWindowManager = viewModelWindowManager;
            _publishEvent = publishEvent;
            _messageRepository = messageRepository;

            MessageListViewModel = messageListViewModelFactory();
            MessageDetailViewModel = messageDetailViewModelFactory();

            SetupObservables();
        }

        public MessageListViewModel MessageListViewModel { get; private set; }

        public MessageDetailViewModel MessageDetailViewModel { get; private set; }

        public bool IsDeactivated
        {
            get { return _isDeactivated; }
            set
            {
                _isDeactivated = value;
                NotifyOfPropertyChange(() => IsDeactivated);
            }
        }

        public string WindowTitle
        {
            get { return _windowTitle; }
            set
            {
                _windowTitle = value;
                NotifyOfPropertyChange(() => WindowTitle);
            }
        }

        public string Version
        {
            get
            {
                return string.Format(
                    "Papercut v{0}",
                    Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            }
        }

        void IHandle<ShowMainWindowEvent>.Handle(ShowMainWindowEvent message)
        {
            if (!_window.IsVisible) _window.Show();

            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;

            _window.Activate();

            _window.Topmost = true;
            _window.Topmost = false;

            _window.Focus();

            if (message.SelectMostRecentMessage) MessageListViewModel.SetSelectedIndex();
        }

        void IHandle<ShowMessageEvent>.Handle(ShowMessageEvent message)
        {
            MessageBox.Show(message.MessageText, message.Caption);
        }

        void IHandle<ShowOptionWindowEvent>.Handle(ShowOptionWindowEvent message)
        {
            ShowOptions();
        }

        void IHandle<SmtpServerBindFailedEvent>.Handle(SmtpServerBindFailedEvent message)
        {
            MessageBox.Show(
                "Failed to start SMTP server listening. The IP and Port combination is in use by another program. To fix, change the server bindings in the options.",
                "Failed");

            ShowOptions();
        }

        void SetupObservables()
        {
            MessageListViewModel.GetPropertyValues(m => m.SelectedMessage)
                .Throttle(TimeSpan.FromMilliseconds(200), TaskPoolScheduler.Default)
                .ObserveOnDispatcher()
                .Subscribe(
                    m =>
                    MessageDetailViewModel.LoadMessageEntry(MessageListViewModel.SelectedMessage));
        }

        public void GoToSite()
        {
            Process.Start("http://papercut.codeplex.com/");
        }

        public void ShowRulesConfiguration()
        {
            _viewModelWindowManager.ShowDialogWithViewModel<RulesConfigurationViewModel>();
        }

        public void ShowOptions()
        {
            _viewModelWindowManager.ShowDialogWithViewModel<OptionsViewModel>();
        }

        public void Exit()
        {
            _publishEvent.Publish(new AppForceShutdownEvent());
        }

        public void ForwardSelected()
        {
            if (MessageListViewModel.SelectedMessage == null) return;

            var forwardViewModel = new ForwardViewModel() { FromSetting = true };
            var result = _viewModelWindowManager.ShowDialog(forwardViewModel);
            if (result == null || !result.Value) return;

            MessageDetailViewModel.IsLoading = true;
            var progressController =
                _window.ShowProgressAsync("Forwarding Email...", "Please wait");

            Observable.Start(
                () =>
                {
                    progressController.Result.SetCancelable(false);
                    progressController.Result.SetIndeterminate();

                    // send message...
                    var session = new SmtpSession
                    {
                        MailFrom = forwardViewModel.From,
                        Sender = forwardViewModel.Server
                    };
                    session.Recipients.Add(forwardViewModel.To);
                    session.Message =
                        _messageRepository.GetMessage(MessageListViewModel.SelectedMessage);

                    new SmtpClient(session).Send();
                    progressController.Result.CloseAsync().Wait();

                    return true;
                }, TaskPoolScheduler.Default)
                .ObserveOnDispatcher()
                .Subscribe((b) =>
                {
                    MessageDetailViewModel.IsLoading = false;
                });

        }

        protected override void OnViewAttached(object view, object context)
        {
            base.OnViewAttached(view, context);

            _window = view as MetroWindow;

            if (_window == null) return;

            _window.StateChanged += (sender, args) =>
            {
                // Hide the window if minimized so it doesn't show up on the task bar
                if (_window.WindowState == WindowState.Minimized) _window.Hide();
            };

            _window.Closing += (sender, args) =>
            {
                if (Application.Current.ShutdownMode == ShutdownMode.OnExplicitShutdown) return;

                // Cancel close and minimize if setting is set to minimize on close
                if (Settings.Default.MinimizeOnClose)
                {
                    args.Cancel = true;
                    _window.WindowState = WindowState.Minimized;
                }
            };

            _window.Activated += (sender, args) => IsDeactivated = false;
            _window.Deactivated += (sender, args) => IsDeactivated = true;

            // Minimize if set to
            if (Settings.Default.StartMinimized)
            {
                bool initialWindowActivate = true;
                _window.Activated += (sender, args) =>
                {
                    if (initialWindowActivate)
                    {
                        initialWindowActivate = false;
                        _window.WindowState = WindowState.Minimized;
                    }
                };
            }
        }
    }
}