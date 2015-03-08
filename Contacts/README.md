## ContactsPlugin for Xamarin and Windows

Simple cross platform plugin to get Contacts from the device.

Ported from [Xamarin.Mobile](http://www.github.com/xamarin/xamarin.mobile) to a cross platform API.

### Setup
* Currently in Alpha (turn on pre-release packages)
* Available on NuGet: http://www.nuget.org/packages/Xam.Plugin.Contacts
* Install into your PCL project and Client projects.

**Supports**
* Xamarin.iOS
* Xamarin.iOS (x64 Unified)
* Xamarin.Android
* Windows Phone 8 (Silverlight)

### API Usage Example
if(await CrossContacts.Current.RequestPermission())
      {
     
        List<Contact> contacts = null;
        CrossContacts.Current.PreferContactAggregation = false;//recommended
//run in background
        await Task.Run(() =>
        {
          if(CrossContacts.Current.Contacts == null)
            return;

          contacts = CrossContacts.Current.Contacts
            .Where(c => !string.IsNullOrWhiteSpace(c.LastName) && c.Phones.Count > 0)         
            .ToList();

          contacts = contacts.OrderBy(c => c.LastName).ToList();
        });
      }




### Important

**Android**
You must add android.permissions.READ_CONTACTS

**Windows Phone**
You must add ID_CAP_CONTACTS permission

#### Contributors
* [jamesmontemagno](https://github.com/jamesmontemagno)

Thanks!

#### License
﻿//
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
