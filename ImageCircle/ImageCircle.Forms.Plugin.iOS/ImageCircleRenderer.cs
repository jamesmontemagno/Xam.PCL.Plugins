﻿using ImageCircle.Forms.Plugin.Abstractions;
using System;
using Xamarin.Forms;
using ImageCircle.Forms.Plugin.iOS;
using Xamarin.Forms.Platform.iOS;
using System.ComponentModel;
using System.Diagnostics;

[assembly: ExportRenderer(typeof(ImageCircle.Forms.Plugin.Abstractions.CircleImage), typeof(ImageCircleRenderer))]
namespace ImageCircle.Forms.Plugin.iOS
{
  /// <summary>
  /// ImageCircle Implementation
  /// </summary>
  public class ImageCircleRenderer : ImageRenderer
  {
    /// <summary>
    /// Used for registration with dependency service
    /// </summary>
    public static void Init() { }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    protected override void OnElementChanged(ElementChangedEventArgs<Image> e)
    {
      base.OnElementChanged(e);
      if (Element == null)
        return;
      CreateCircle();
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      base.OnElementPropertyChanged(sender, e);
      if (e.PropertyName == VisualElement.HeightProperty.PropertyName ||
          e.PropertyName == VisualElement.WidthProperty.PropertyName ||
        e.PropertyName == ImageCircle.Forms.Plugin.Abstractions.CircleImage.BorderColorProperty.PropertyName ||
        e.PropertyName == ImageCircle.Forms.Plugin.Abstractions.CircleImage.BorderThicknessProperty.PropertyName)
      {
        CreateCircle();
      }
    }

    private void CreateCircle()
    {
      try
      {
        double min = Math.Min(Element.Width, Element.Height);
        Control.Layer.CornerRadius = (float)(min / 2.0);
        Control.Layer.MasksToBounds = false;
        Control.Layer.BorderColor = ((ImageCircle.Forms.Plugin.Abstractions.CircleImage)Element).BorderColor.ToCGColor();
        Control.Layer.BorderWidth = ((ImageCircle.Forms.Plugin.Abstractions.CircleImage)Element).BorderThickness;
        Control.ClipsToBounds = true;
      }
      catch(Exception ex)
      {
        Debug.WriteLine("Unable to create circle image: " + ex);
      }
    }
  }
}
