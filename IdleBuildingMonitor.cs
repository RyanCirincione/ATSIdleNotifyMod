using Eremite.Buildings;
using Eremite.Services;
using Eremite.Services.Monitors;
using System;
using UniRx;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace IdleNotify {
    class IdleBuildingMonitor : GameMonitor {
        public override bool IsEnabled() {
            return true;
        }

        public override void OnInit() {
            (from building in GameBlackboardService.FinishedBuildingRemoved
             where building is ProductionBuilding
             select building).Subscribe(new Action<Building>(this.ProductionBuildingRemoved)).AddTo(this.disposables);
        }

        private void ProductionBuildingRemoved(Building building) {
            ProductionBuilding prod = building as ProductionBuilding;
            if(this.IsShowing(prod)) {
                this.HideAlert(prod);
            }
        }

        public override void OnSlowUpdate() {
            foreach (ProductionBuilding building in Serviceable.BuildingsService.ProductionBuildings) {
                if (building.IsFinished()) {
                    this.UpdateBuilding(building);
                }
            }
        }

        private void UpdateBuilding(ProductionBuilding building) {
            this.CheckForDequeue(building);
            this.CheckForQueue(building);
            this.CheckForHiding(building);
            this.CheckForShowing(building);
        }

        private void CheckForDequeue(ProductionBuilding building) {
            if (!this.IsQueued(building)) {
                return;
            }

            if (!this.IsBuildingIdle(building)) {
                this.queue.Remove(building);
            }
        }

        private void CheckForQueue(ProductionBuilding building) {
            if (this.IsShowing(building) || this.IsQueued(building)) {
                return;
            }
            if (this.IsBuildingIdle(building)) {
                this.queue.Add(building, Serviceable.GameTime);
            }
        }

        private void CheckForHiding(ProductionBuilding building) {
            if (!this.IsShowing(building)) {
                return;
            }
            if (!this.IsEnabled() || !this.IsBuildingIdle(building)) {
                this.HideAlert(building);
            }
        }

        private void CheckForShowing(ProductionBuilding building) {
            if (this.IsShowing(building) || !this.IsQueued(building)) {
                return;
            }
            if (!this.IsEnabled()) {
                return;
            }
            if (this.ShouldShow(building)) {
                this.ShowAlert(building, this.CreateAlert(building));
            }
        }

        private void ShowAlert(ProductionBuilding building, MonitorAlert alert) {
            this.current.Add(building, alert);
            base.AddAlert(alert);
        }

        private void HideAlert(ProductionBuilding building) {
            base.RemoveAlert(this.current[building]);
            this.current.Remove(building);
        }

        private MonitorAlert CreateAlert(ProductionBuilding building) {
            return new MonitorAlert {
                severity = AlertSeverity.Warning,
                //icon = base.Config.noNearbyDepositIcon,
                text = $"Worker idle in {building.BuildingModel.displayName}",
                description = $"At least one worker is idle in your {building.BuildingModel.displayName}. Click here to go to it.",
                showTime = base.GetShowTime(),
                clickCallback = new Action<MonitorAlert>(this.OnAlertClicked)
            };
        }

        private void OnAlertClicked(MonitorAlert alert) {
            this.Focus(this.current.First((KeyValuePair<ProductionBuilding, MonitorAlert> a) => a.Value == alert).Key);
        }

        private void Focus(ProductionBuilding building) {
            GameInputService.Focus(building, true);
        }

        private bool IsShowing(ProductionBuilding building) {
            return this.current.ContainsKey(building);
        }

        private bool IsBuildingIdle(ProductionBuilding building) {
            return building.IsBuildingIdle();
        }

        private bool IsQueued(ProductionBuilding building) {
            return this.queue.ContainsKey(building);
        }

        private bool ShouldShow(ProductionBuilding building) {
            // This 0 means we want no delay in being alerted about idle buildings
            return Serviceable.GameTime - this.queue[building] >= 0;
        }

        private Dictionary<ProductionBuilding, MonitorAlert> current = new Dictionary<ProductionBuilding, MonitorAlert>();
        private Dictionary<ProductionBuilding, float> queue = new Dictionary<ProductionBuilding, float>();
    }
}
