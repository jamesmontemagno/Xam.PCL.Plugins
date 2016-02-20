
//
//  Copyright 2011-2013, Xamarin Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//

using System;

using System.Threading.Tasks;
using System.Threading;
#if __UNIFIED__
using CoreLocation;
using Foundation;
using UIKit;
#else
using MonoTouch.CoreLocation;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
#endif
using Plugin.Geolocator.Abstractions;


namespace Plugin.Geolocator
{
    /// <summary>
    /// Implementation for Geolocator
    /// </summary>
    public class GeolocatorImplementation : IGeolocator
    {
		bool deferringUpdates = false;

        public GeolocatorImplementation()
        {
            DesiredAccuracy = 100;
            this.manager = GetManager();
            this.manager.AuthorizationChanged += OnAuthorizationChanged;
            this.manager.Failed += OnFailed;

            if (UIDevice.CurrentDevice.CheckSystemVersion(6, 0))
                this.manager.LocationsUpdated += OnLocationsUpdated;
            else
                this.manager.UpdatedLocation += OnUpdatedLocation;

            this.manager.UpdatedHeading += OnUpdatedHeading;

			this.manager.DeferredUpdatesFinished += (o, e) => 
			{
				this.deferringUpdates = false;
			};

            RequestAuthorization();
        }

        private void RequestAuthorization()
        {
            var info = NSBundle.MainBundle.InfoDictionary;

            if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
            {
                if (info.ContainsKey(new NSString("NSLocationWhenInUseUsageDescription")))
                    this.manager.RequestWhenInUseAuthorization();
                else if (info.ContainsKey(new NSString("NSLocationAlwaysUsageDescription")))
                    this.manager.RequestAlwaysAuthorization();
                else
                    throw new UnauthorizedAccessException("On iOS 8.0 and higher you must set either NSLocationWhenInUseUsageDescription or NSLocationAlwaysUsageDescription in your Info.plist file to enable Authorization Requests for Location updates!");
            }
        }
        /// <inheritdoc/>
        public event EventHandler<PositionErrorEventArgs> PositionError;
        /// <inheritdoc/>
        public event EventHandler<PositionEventArgs> PositionChanged;
        /// <inheritdoc/>
        public double DesiredAccuracy
        {
            get;
            set;
        }
        /// <inheritdoc/>
        public bool IsListening
        {
            get { return this.isListening; }
        }
        /// <inheritdoc/>
        public bool SupportsHeading
        {
            get { return CLLocationManager.HeadingAvailable; }
        }

        bool pausesLocationUpdatesAutomatically;
        /// <inheritdoc/>
        public bool PausesLocationUpdatesAutomatically
        {
            get
            {
                return pausesLocationUpdatesAutomatically;
            }
            set
            {
                pausesLocationUpdatesAutomatically = value;
                if (UIDevice.CurrentDevice.CheckSystemVersion(6, 0))
                {
                    if (this.manager != null)
                        this.manager.PausesLocationUpdatesAutomatically = value;
                }
            }
        }

		EnergySettings energySettings;

        bool allowsBackgroundUpdates;

        /// <inheritdoc/>
        public bool AllowsBackgroundUpdates
        {
            get
            {
                return allowsBackgroundUpdates;
            }
            set
            {
                allowsBackgroundUpdates = value;
                if (UIDevice.CurrentDevice.CheckSystemVersion(9, 0))
                {
                    if (this.manager != null)
                        this.manager.AllowsBackgroundLocationUpdates = value;
                }
            }
        }

        /// <inheritdoc/>
        public bool IsGeolocationAvailable
        {
            get { return true; } // all iOS devices support at least wifi geolocation
        }
        /// <inheritdoc/>
        public bool IsGeolocationEnabled
        {
            get
            {
                var status = CLLocationManager.Status;

                if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
                {
                    return status == CLAuthorizationStatus.AuthorizedAlways
                    || status == CLAuthorizationStatus.AuthorizedWhenInUse;
                }
                else {
                    return status == CLAuthorizationStatus.Authorized;
                }
            }
        }


        /// <inheritdoc/>
        public Task<Position> GetPositionAsync(int timeoutMilliseconds = Timeout.Infinite, CancellationToken? cancelToken = null, bool includeHeading = false)
        {
            if (timeoutMilliseconds <= 0 && timeoutMilliseconds != Timeout.Infinite)
                throw new ArgumentOutOfRangeException("timeoutMilliseconds", "Timeout must be positive or Timeout.Infinite");

            if (!cancelToken.HasValue)
                cancelToken = CancellationToken.None;

            TaskCompletionSource<Position> tcs;
            if (!IsListening)
            {
                var m = GetManager();

                if (UIDevice.CurrentDevice.CheckSystemVersion(9, 0))
                {
                    m.AllowsBackgroundLocationUpdates = AllowsBackgroundUpdates;
                }

                if (UIDevice.CurrentDevice.CheckSystemVersion(6, 0))
                {
                    m.PausesLocationUpdatesAutomatically = PausesLocationUpdatesAutomatically;
                }

                tcs = new TaskCompletionSource<Position>(m);
                var singleListener = new GeolocationSingleUpdateDelegate(m, DesiredAccuracy, includeHeading, timeoutMilliseconds, cancelToken.Value);
                m.Delegate = singleListener;

                m.StartUpdatingLocation();
                if (includeHeading && SupportsHeading)
                    m.StartUpdatingHeading();

                return singleListener.Task;
            }
            else
            {
                tcs = new TaskCompletionSource<Position>();
                if (this.position == null)
                {
                    EventHandler<PositionErrorEventArgs> gotError = null;
                    gotError = (s, e) =>
                    {
                        tcs.TrySetException(new GeolocationException(e.Error));
                        PositionError -= gotError;
                    };

                    PositionError += gotError;

                    EventHandler<PositionEventArgs> gotPosition = null;
                    gotPosition = (s, e) =>
                    {
                        tcs.TrySetResult(e.Position);
                        PositionChanged -= gotPosition;
                    };

                    PositionChanged += gotPosition;
                }
                else
                    tcs.SetResult(this.position);
            }

            return tcs.Task;
        }
        /// <inheritdoc/>
		public Task<bool> StartListeningAsync(int minTime, double minDistance, bool includeHeading = false, EnergySettings energySettings = null)
        {
			this.energySettings = energySettings;
			
            if (minTime < 0)
                throw new ArgumentOutOfRangeException("minTime");
            if (minDistance < 0)
                throw new ArgumentOutOfRangeException("minDistance");
            if (this.isListening)
                throw new InvalidOperationException("Already listening");

			double desiredAccuracy = DesiredAccuracy;

			// to use deferral, CLLocationManager.DistanceFilter must be set to CLLocationDistance.None, and CLLocationManager.DesiredAccuracy must be 
			// either CLLocation.AccuracyBest or CLLocation.AccuracyBestForNavigation. deferral only available on iOS 6.0 and above.
			if (this.energySettings != null && this.energySettings.DeferLocationUpdates && UIDevice.CurrentDevice.CheckSystemVersion (6, 0)) 
			{
				minDistance = CLLocationDistance.FilterNone;
				desiredAccuracy = CLLocation.AccuracyBest;
			}

            this.isListening = true;
			this.manager.DesiredAccuracy = desiredAccuracy;
            this.manager.DistanceFilter = minDistance;

			if (this.energySettings != null && this.energySettings.ListenForSignificantChanges)
				this.manager.StartMonitoringSignificantLocationChanges();
			else
				this.manager.StartUpdatingLocation();

            if (includeHeading && CLLocationManager.HeadingAvailable)
                this.manager.StartUpdatingHeading();

            return Task.FromResult(true);
        }
        /// <inheritdoc/>
        public Task<bool> StopListeningAsync()
        {
            if (!this.isListening)
                return Task.FromResult(true);

            this.isListening = false;
            if (CLLocationManager.HeadingAvailable)
                this.manager.StopUpdatingHeading();

			// it looks like deferred location updates can apply to the standard service or significant change service. disallow deferral in either case.
			if (this.energySettings != null && this.energySettings.DeferLocationUpdates && UIDevice.CurrentDevice.CheckSystemVersion (6, 0))
				this.manager.DisallowDeferredLocationUpdates ();
			
			if (this.energySettings != null && this.energySettings.ListenForSignificantChanges)
				this.manager.StopMonitoringSignificantLocationChanges();
			else
				this.manager.StopUpdatingLocation();

			this.energySettings = null;
            this.position = null;

            return Task.FromResult(true);
        }

        private readonly CLLocationManager manager;
        private bool isListening;
        private Position position;

        private CLLocationManager GetManager()
        {
            CLLocationManager m = null;
            new NSObject().InvokeOnMainThread(() => m = new CLLocationManager());
            return m;
        }

        private void OnUpdatedHeading(object sender, CLHeadingUpdatedEventArgs e)
        {
            if (e.NewHeading.TrueHeading == -1)
                return;

            Position p = (this.position == null) ? new Position() : new Position(this.position);

            p.Heading = e.NewHeading.TrueHeading;

            this.position = p;

            OnPositionChanged(new PositionEventArgs(p));
        }

        private void OnLocationsUpdated(object sender, CLLocationsUpdatedEventArgs e)
        {
            foreach (CLLocation location in e.Locations)
                UpdatePosition(location);

			// defer future location updates if requested
			if (this.energySettings != null && this.energySettings.DeferLocationUpdates && !this.deferringUpdates && UIDevice.CurrentDevice.CheckSystemVersion (6, 0)) 
			{
				this.manager.AllowDeferredLocationUpdatesUntil (this.energySettings.DeferralDistanceMeters == null ? CLLocationDistance.MaxDistance : this.energySettings.DeferralDistanceMeters.GetValueOrDefault (), 
					                                            this.energySettings.DeferralTime == null ? CLLocationManager.MaxTimeInterval : this.energySettings.DeferralTime.GetValueOrDefault ().TotalSeconds);
				this.deferringUpdates = true;
			}			
        }

        private void OnUpdatedLocation(object sender, CLLocationUpdatedEventArgs e)
        {
            UpdatePosition(e.NewLocation);
        }

        private void UpdatePosition(CLLocation location)
        {
            Position p = (this.position == null) ? new Position() : new Position(this.position);

            if (location.HorizontalAccuracy > -1)
            {
                p.Accuracy = location.HorizontalAccuracy;
                p.Latitude = location.Coordinate.Latitude;
                p.Longitude = location.Coordinate.Longitude;
            }

            if (location.VerticalAccuracy > -1)
            {
                p.Altitude = location.Altitude;
                p.AltitudeAccuracy = location.VerticalAccuracy;
            }

            if (location.Speed > -1)
                p.Speed = location.Speed;

            var dateTime = location.Timestamp.ToDateTime().ToUniversalTime();
            p.Timestamp = new DateTimeOffset(dateTime);

            this.position = p;

            OnPositionChanged(new PositionEventArgs(p));

            location.Dispose();
        }


        private void OnFailed(object sender, NSErrorEventArgs e)
        {
            if ((CLError)(int)e.Error.Code == CLError.Network)
                OnPositionError(new PositionErrorEventArgs(GeolocationError.PositionUnavailable));
        }

        private void OnAuthorizationChanged(object sender, CLAuthorizationChangedEventArgs e)
        {
            if (e.Status == CLAuthorizationStatus.Denied || e.Status == CLAuthorizationStatus.Restricted)
                OnPositionError(new PositionErrorEventArgs(GeolocationError.Unauthorized));
        }

        private void OnPositionChanged(PositionEventArgs e)
        {
            var changed = PositionChanged;
            if (changed != null)
                changed(this, e);
        }

        private async void OnPositionError(PositionErrorEventArgs e)
        {
            await StopListeningAsync();

            var error = PositionError;
            if (error != null)
                error(this, e);
        }
    }
}