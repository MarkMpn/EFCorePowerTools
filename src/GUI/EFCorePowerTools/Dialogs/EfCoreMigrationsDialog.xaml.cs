﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EFCorePowerTools.Extensions;
using EFCorePowerTools.Handlers;
using EnvDTE;
using ErikEJ.SqlCeToolbox.Helpers;
using Microsoft.VisualStudio.Shell.Interop;

namespace ErikEJ.SqlCeToolbox.Dialogs
{
    public partial class EfCoreMigrationsDialog
    {
        private SortedDictionary<string, string> _statusList;
        private readonly EFCorePowerTools.EFCorePowerToolsPackage _package;
        private readonly ProcessLauncher _processLauncher = new ProcessLauncher();
        private readonly string _outputPath;
        private readonly bool _isNetCore;
        private readonly Project _project;
        private object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Build;

        public EfCoreMigrationsDialog(EFCorePowerTools.EFCorePowerToolsPackage package, string outputPath, bool isNetCore, Project project)
        {
            Telemetry.TrackPageView(nameof(EfCoreModelDialog));
            InitializeComponent();
            Background = VsThemes.GetWindowBackground();
            _package = package;
            _isNetCore = isNetCore;
            _outputPath = outputPath;
            _project = project;
        }

        public string ProjectName
        {
            set
            {
                Title = $"Manage Migrations in Project {value}";
            }
        }

        private void CmbDbContext_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbDbContext.SelectedItem != null)
            {
                var status = _statusList[cmbDbContext.SelectedValue.ToString()];
                SetUI(status);
            };
        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            imgUnicorn.Opacity = 0;
            txtMigrationName.Visibility = Visibility.Collapsed;
            lblMigration.Visibility = Visibility.Collapsed;
            btnApply.Visibility = Visibility.Collapsed;

            var image = new Image();
            var thisassembly = Assembly.GetExecutingAssembly();
            var imageStream = thisassembly.GetManifestResourceStream("EFCorePowerTools.Resources.Unicorn.png");
            var bmp = BitmapFrame.Create(imageStream);
            image.Source = bmp;
            imgUnicorn.ImageSource = image.Source;
            await GetMigrationStatus();
        }

        private async void btnApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartAnimation();
                btnApply.IsEnabled = false;

                if (btnApply.Content.ToString() == "Add Migration")
                {
                    if (string.IsNullOrEmpty(txtMigrationName.Text))
                    {
                        EnvDteHelper.ShowError("Migration Name required");
                        return;
                    }

                    _package.Dte2.StatusBar.Text = $"Creating Migration {txtMigrationName.Text} in DbContext {cmbDbContext.SelectedValue.ToString()}";
                    var processResult = await _processLauncher.GetOutputAsync(_outputPath, Path.GetDirectoryName(_project.FullName), _isNetCore, GenerationType.MigrationAdd, cmbDbContext.SelectedValue.ToString(), txtMigrationName.Text, _project.Properties.Item("DefaultNamespace").Value.ToString());

                    var result = BuildModelResult(processResult);

                    if (processResult.StartsWith("Error:"))
                    {
                        EnvDteHelper.ShowError(processResult);
                        return;
                    }

                    if (result.Count == 1)
                    {
                        string[] lines = result.First().Value.Split(
                                new[] { Environment.NewLine },
                                StringSplitOptions.None
                            );
                        if (lines.Length == 3)
                        {
                            _project.ProjectItems.AddFromFile(lines[1]); // migrationFile
                            _package.Dte2.ItemOperations.OpenFile(lines[1]); // migrationFile

                            _project.ProjectItems.AddFromFile(lines[0]); // metadataFile
                            _project.ProjectItems.AddFromFile(lines[2]); // snapshotFile
                        }
                    }
                }

                if (btnApply.Content.ToString() == "Update Database")
                {
                    _package.Dte2.StatusBar.Text = $"Updating Database from migrations in DbContext {cmbDbContext.SelectedValue.ToString()}";
                    var processResult = await _processLauncher.GetOutputAsync(_outputPath, _isNetCore, GenerationType.MigrationApply, cmbDbContext.SelectedValue.ToString());
                    if (processResult.StartsWith("Error:"))
                    {
                        EnvDteHelper.ShowError(processResult);
                        return;
                    }
                }

                await GetMigrationStatus();
            }
            catch (Exception ex)
            {
                EnvDteHelper.ShowError(ex.ToString());
            }
            finally
            {
                StopAnimation();
                _package.Dte2.StatusBar.Text = string.Empty;

                btnApply.IsEnabled = true;
            }
        }

        private void SetUI(string status)
        {
            imgUnicorn.Opacity = 0;
            txtMigrationName.Visibility = Visibility.Visible;
            lblMigration.Visibility = Visibility.Visible;
            btnApply.Visibility = Visibility.Visible;

            // InSync, NoMigrations, Changes, Pending
            if (status == "InSync")
            {
                txtMessage.Text = $"Your database and model are in sync.";
                lblMigration.Visibility = Visibility.Collapsed;
                txtMigrationName.Visibility = Visibility.Collapsed;
                btnApply.Visibility = Visibility.Collapsed;
                imgUnicorn.Opacity = 0.2;
            }

            if (status == "NoMigrations")
            {
                txtMessage.Text = $"No migrations are present in your project, create your initial migration.{Environment.NewLine}Enter a name for the new migration below.";
                lblMigration.Text = "Migration Name";
                btnApply.Content = "Add Migration";
            }

            if (status == "Changes")
            {
                txtMessage.Text = $"There are pending model changes, add a migration with the changes.{Environment.NewLine}Enter a name for the migration below.";
                lblMigration.Text = "Migration Name";
                btnApply.Content = "Add Migration";
            }

            if (status == "Pending")
            {
                txtMessage.Text = $"There are migrations that have not been applied to the database.";
                lblMigration.Visibility = Visibility.Collapsed;
                txtMigrationName.Visibility = Visibility.Collapsed;
                btnApply.Content = "Update Database";
            }
        }

        private void StartAnimation()
        {
            IVsStatusbar statusBar = (IVsStatusbar)_package.GetService<SVsStatusbar>();
            statusBar.Animation(1, ref icon);
        }

        private void StopAnimation()
        {
            IVsStatusbar statusBar = (IVsStatusbar)_package.GetService<SVsStatusbar>();
            statusBar.Animation(0, ref icon);
        }

        private async Task GetMigrationStatus()
        {
            try
            {
                StartAnimation();
                _package.Dte2.StatusBar.Text = "Getting Migration Status";
                if (_project.TryBuild())
                {
                    var processResult = await _processLauncher.GetOutputAsync(_outputPath, _isNetCore, GenerationType.MigrationStatus, null);

                    ReportStatus(processResult);
                }
                else
                {
                    EnvDteHelper.ShowError("Build failed");
                }
            }
            catch (Exception ex)
            {
                EnvDteHelper.ShowError(ex.ToString());
            }
            finally
            {
                _package.Dte2.StatusBar.Text = string.Empty;
                StopAnimation();
            }
        }

        private void ReportStatus(string processResult)
        {
            _package.Dte2.StatusBar.Text = string.Empty;

            if (processResult.StartsWith("Error:"))
            {
                EnvDteHelper.ShowError(processResult);
                return;
            }

            var result = BuildModelResult(processResult);
            UpdateStatusList(result);
        }

        private void UpdateStatusList(SortedDictionary<string, string> statusList)
        {
            _statusList = statusList;
            cmbDbContext.ItemsSource = _statusList.Select(s => s.Key).ToList();
            cmbDbContext.SelectionChanged += CmbDbContext_SelectionChanged;
            if (_statusList.Count > 0)
            {
                cmbDbContext.SelectedIndex = 0;
            }
        }

        private SortedDictionary<string, string> BuildModelResult(string modelInfo)
        {
            var result = new SortedDictionary<string, string>();

            var contexts = modelInfo.Split(new[] { "DbContext:" + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var context in contexts)
            {
                var parts = context.Split(new[] { "DebugView:" + Environment.NewLine }, StringSplitOptions.None);
                result.Add(parts[0].Trim(), parts[1].Trim());
            }

            return result;
        }
    }
}
