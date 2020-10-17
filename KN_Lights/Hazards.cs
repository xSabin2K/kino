using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KN_Core;
using KN_Loader;
using SyncMultiplayer;
using UnityEngine;

namespace KN_Lights {
  public class Hazards {
    private const string DefaultConfigFile = "kn_default_hazards.knl";

    private HazardLights ownHazards_;
    private readonly List<HazardLights> hazards_;
    private readonly List<HazardLights> defaultHazards_;


    private readonly Core core_;

    public Hazards(Core core) {
      core_ = core;

      hazards_ = new List<HazardLights>();
      defaultHazards_ = new List<HazardLights>();
    }

    public void OnStart() {
      var assembly = Assembly.GetExecutingAssembly();

      var stream = Embedded.LoadEmbeddedFile(assembly, $"KN_Lights.Resources.{DefaultConfigFile}");
      if (stream != null) {
        using (stream) {
          if (DataSerializer.Deserialize<HazardLights>("KN_CarLights", stream, out var defaultData)) {
            defaultHazards_.AddRange(defaultData.ConvertAll(d => (HazardLights) d));
          }
        }
      }
    }

    public void OnStop() {
#if KN_DEV_TOOLS
      DataSerializer.Serialize("KN_CarLights", hazards_.ToList<ISerializable>(), KnConfig.BaseDir + "dev/" + DefaultConfigFile, Loader.Version);
#endif
    }

    public void OnCarLoaded(KnCar car) {
      bool found = hazards_.Any(hz => hz.Car == car);
      if (!found) {
        var hz = GetOrCreateHazards(car.Id);
        hz.Attach(car);
        hazards_.Add(hz);

        if (car == core_.PlayerCar) {
          ownHazards_ = hz;
        }
      }

      SendData();
    }

    public void OnUdpData(SmartfoxDataPackage data) {
      int id = data.Data.GetInt("id");

      foreach (var hz in hazards_) {
        if (!KnCar.IsNull(hz.Car) && hz.Car.IsNetworkCar && hz.Car.Base.networkPlayer.NetworkID == id) {
          hz.OnUdpData(data);
          break;
        }
      }
    }

    public void Update(float discardDistance) {
      hazards_.RemoveAll(hz => {
        if (KnCar.IsNull(hz.Car)) {
          if (hz == ownHazards_) {
            ToggleHazards(true);
            ownHazards_ = null;
          }
          hz.Dispose();
          return true;
        }
        return false;
      });

      if ((core_.IsCarChanged || ownHazards_ == null) && !KnCar.IsNull(core_.PlayerCar)) {
        var hz = GetOrCreateHazards(core_.PlayerCar.Id);
        hz.Attach(core_.PlayerCar);
        hazards_.Add(hz);
        ownHazards_ = hz;
      }

      foreach (var hz in hazards_) {
        if (!hz.Hazard) {
          hz.Enabled = false;
        }
      }

      if (!core_.IsGuiEnabled && ownHazards_ != null && ownHazards_.Debug) {
        ownHazards_.Debug = false;
      }

      Optimize(discardDistance);

      if (Controls.KeyDown("toggle_hazards")) {
        ToggleHazards(false);
      }
    }

    public void LateUpdate() {
      foreach (var hz in hazards_) {
        hz.LateUpdate();
      }
    }

    public void OnGui(Gui gui, ref float x, ref float y, float width, float height) {
      bool hazard = ownHazards_?.Hazard ?? false;
      if (gui.TextButton(ref x, ref y, width, height, Locale.Get("hazard_lights"), hazard ? Skin.ButtonSkin.Active : Skin.ButtonSkin.Normal)) {
        ToggleHazards(false);
      }

#if KN_DEV_TOOLS
      gui.Line(x, y, width, 1.0f, Skin.SeparatorColor);
      y += Gui.Offset;

      bool hzEnabled = ownHazards_?.Enabled ?? false;
      if (gui.TextButton(ref x, ref y, width, height, "HAZARD LIGHTS ENABLED", hzEnabled ? Skin.ButtonSkin.Active : Skin.ButtonSkin.Normal)) {
        if (ownHazards_ != null) {
          ownHazards_.Enabled = !ownHazards_.Enabled;
        }
      }

      bool debugObjects = ownHazards_?.Debug ?? false;
      if (gui.TextButton(ref x, ref y, width, height, "DEBUG OBJECTS", debugObjects ? Skin.ButtonSkin.Active : Skin.ButtonSkin.Normal)) {
        if (ownHazards_ != null) {
          ownHazards_.Debug = !ownHazards_.Debug;
        }
      }

      float brightness = ownHazards_?.Brightness ?? 0.0f;
      if (gui.SliderH(ref x, ref y, width, ref brightness, 1.0f, 20.0f, $"HAZARD LIGHT BRIGHTNESS: {brightness:F1}")) {
        if (ownHazards_ != null) {
          ownHazards_.Brightness = brightness;
        }
      }

      float range = ownHazards_?.Range ?? 0.0f;
      if (gui.SliderH(ref x, ref y, width, ref range, 0.05f, 1.0f, $"HAZARD LIGHT RANGE: {range:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.Range = range;
        }
      }

      var offset = ownHazards_?.OffsetFront ?? Vector3.zero;
      if (gui.SliderH(ref x, ref y, width, ref offset.x, Lights.MinPosBound, Lights.MaxPosBound, $"FRONT X: {offset.x:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.OffsetFront = offset;
        }
      }

      if (gui.SliderH(ref x, ref y, width, ref offset.y, Lights.MinPosBound, Lights.MaxPosBound, $"FRONT Y: {offset.y:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.OffsetFront = offset;
        }
      }

      if (gui.SliderH(ref x, ref y, width, ref offset.z, Lights.MinPosBoundZ, Lights.MaxPosBoundZ, $"FRONT Z: {offset.z:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.OffsetFront = offset;
        }
      }

      offset = ownHazards_?.OffsetRear ?? Vector3.zero;
      if (gui.SliderH(ref x, ref y, width, ref offset.x, Lights.MinPosBound, Lights.MaxPosBound, $"REAR X: {offset.x:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.OffsetRear = offset;
        }
      }

      if (gui.SliderH(ref x, ref y, width, ref offset.y, Lights.MinPosBound, Lights.MaxPosBound, $"REAR Y: {offset.y:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.OffsetRear = offset;
        }
      }

      if (gui.SliderH(ref x, ref y, width, ref offset.z, -Lights.MinPosBoundZ, -Lights.MaxPosBoundZ, $"REAR Z: {offset.z:F}")) {
        if (ownHazards_ != null) {
          ownHazards_.OffsetRear = offset;
        }
      }
#endif
    }

    private HazardLights GetOrCreateHazards(int id) {
      foreach (var hz in defaultHazards_) {
        if (hz.Car.Id == id) {
          return hz.Copy();
        }
      }
      Log.Write($"[KN_Lights::Hazards]: Unable to found config for '{id}', creating default");
      return new HazardLights();
    }

    private void Optimize(float distance) {
      var camera = core_.ActiveCamera;
      if (camera != null) {
        foreach (var hz in hazards_) {
          if (!KnCar.IsNull(hz.Car)) {
            hz.Discarded = Vector3.Distance(camera.transform.position, hz.Car.Transform.position) > distance;
          }
        }
      }
    }

    private void ToggleHazards(bool disable) {
      if (ownHazards_ != null) {
        if (disable) {
          ownHazards_.Hazard = false;
        }
        else {
          ownHazards_.Hazard = !ownHazards_.Hazard;
        }
        SendData();
      }
    }

    private void SendData() {
      int id = NetworkController.InstanceGame?.LocalPlayer?.NetworkID ?? -1;
      if (id == -1 || ownHazards_ == null) {
        return;
      }

      ownHazards_.Send(id, core_.Udp);
    }
  }
}