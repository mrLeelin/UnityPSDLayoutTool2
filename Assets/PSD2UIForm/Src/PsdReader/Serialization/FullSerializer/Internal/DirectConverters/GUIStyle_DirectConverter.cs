using System;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
namespace cn.efunstudio.psdreader.FullSerializer.Internal.DirectConverters
{
    public class GUIStyle_DirectConverter : fsDirectConverter<GUIStyle>
    {
        internal static GUIStyle_DirectConverter s_ObfuscationSentinel;

        protected override fsResult DoSerialize(GUIStyle model, Dictionary<string, fsData> serialized)
        {
            return fsResult.Success + SerializeMember<GUIStyleState>(serialized, null, "active", model.active) + SerializeMember<TextAnchor>(serialized, null, "alignment", model.alignment) + SerializeMember<RectOffset>(serialized, null, "border", model.border) + SerializeMember<TextClipping>(serialized, null, "clipping", model.clipping) + SerializeMember<Vector2>(serialized, null, "contentOffset", model.contentOffset) + SerializeMember(serialized, null, "fixedHeight", model.fixedHeight) + SerializeMember(serialized, null, "fixedWidth", model.fixedWidth) + SerializeMember<GUIStyleState>(serialized, null, "focused", model.focused) + SerializeMember<Font>(serialized, null, "font", model.font) + SerializeMember(serialized, null, "fontSize", model.fontSize) + SerializeMember<FontStyle>(serialized, null, "fontStyle", model.fontStyle) + SerializeMember<GUIStyleState>(serialized, null, "hover", model.hover) + SerializeMember<ImagePosition>(serialized, null, "imagePosition", model.imagePosition) + SerializeMember<RectOffset>(serialized, null, "margin", model.margin) + SerializeMember(serialized, null, "name", model.name) + SerializeMember<GUIStyleState>(serialized, null, "normal", model.normal) + SerializeMember<GUIStyleState>(serialized, null, "onActive", model.onActive) + SerializeMember<GUIStyleState>(serialized, null, "onFocused", model.onFocused) + SerializeMember<GUIStyleState>(serialized, null, "onHover", model.onHover) + SerializeMember<GUIStyleState>(serialized, null, "onNormal", model.onNormal) + SerializeMember<RectOffset>(serialized, null, "overflow", model.overflow) + SerializeMember<RectOffset>(serialized, null, "padding", model.padding) + SerializeMember(serialized, null, "richText", model.richText) + SerializeMember(serialized, null, "stretchHeight", model.stretchHeight) + SerializeMember(serialized, null, "stretchWidth", model.stretchWidth) + SerializeMember(serialized, null, "wordWrap", model.wordWrap);
        }

        protected override fsResult DoDeserialize(Dictionary<string, fsData> data, ref GUIStyle model)
        {
            fsResult success = fsResult.Success;
            GUIStyleState value = model.active;
            fsResult fsResultA = success + DeserializeMember<GUIStyleState>(data, null, "active", out value);
            model.active = value;
            TextAnchor value2 = model.alignment;
            fsResult fsResult2 = fsResultA + DeserializeMember<TextAnchor>(data, null, "alignment", out value2);
            model.alignment = value2;
            RectOffset value3 = model.border;
            fsResult fsResult3 = fsResult2 + DeserializeMember<RectOffset>(data, null, "border", out value3);
            model.border = value3;
            TextClipping value4 = model.clipping;
            fsResult fsResult4 = fsResult3 + DeserializeMember<TextClipping>(data, null, "clipping", out value4);
            model.clipping = value4;
            Vector2 value5 = model.contentOffset;
            fsResult fsResult5 = fsResult4 + DeserializeMember<Vector2>(data, null, "contentOffset", out value5);
            model.contentOffset = value5;
            float value6 = model.fixedHeight;
            fsResult fsResult6 = fsResult5 + DeserializeMember<float>(data, null, "fixedHeight", out value6);
            model.fixedHeight = value6;
            float value7 = model.fixedWidth;
            fsResult fsResult7 = fsResult6 + DeserializeMember<float>(data, null, "fixedWidth", out value7);
            model.fixedWidth = value7;
            GUIStyleState value8 = model.focused;
            fsResult fsResult8 = fsResult7 + DeserializeMember<GUIStyleState>(data, null, "focused", out value8);
            model.focused = value8;
            Font value9 = model.font;
            fsResult fsResult9 = fsResult8 + DeserializeMember<Font>(data, null, "font", out value9);
            model.font = value9;
            int value10 = model.fontSize;
            fsResult fsResult10 = fsResult9 + DeserializeMember<int>(data, null, "fontSize", out value10);
            model.fontSize = value10;
            FontStyle value11 = model.fontStyle;
            fsResult fsResult11 = fsResult10 + DeserializeMember<FontStyle>(data, null, "fontStyle", out value11);
            model.fontStyle = value11;
            GUIStyleState value12 = model.hover;
            fsResult fsResult12 = fsResult11 + DeserializeMember<GUIStyleState>(data, null, "hover", out value12);
            model.hover = value12;
            ImagePosition value13 = model.imagePosition;
            fsResult fsResult13 = fsResult12 + DeserializeMember<ImagePosition>(data, null, "imagePosition", out value13);
            model.imagePosition = value13;
            RectOffset value14 = model.margin;
            fsResult fsResult14 = fsResult13 + DeserializeMember<RectOffset>(data, null, "margin", out value14);
            model.margin = value14;
            string value15 = model.name;
            fsResult fsResult15 = fsResult14 + DeserializeMember<string>(data, null, "name", out value15);
            model.name = value15;
            GUIStyleState value16 = model.normal;
            fsResult fsResult16 = fsResult15 + DeserializeMember<GUIStyleState>(data, null, "normal", out value16);
            model.normal = value16;
            GUIStyleState value17 = model.onActive;
            fsResult fsResult17 = fsResult16 + DeserializeMember<GUIStyleState>(data, null, "onActive", out value17);
            model.onActive = value17;
            GUIStyleState value18 = model.onFocused;
            fsResult fsResult18 = fsResult17 + DeserializeMember<GUIStyleState>(data, null, "onFocused", out value18);
            model.onFocused = value18;
            GUIStyleState value19 = model.onHover;
            fsResult fsResult19 = fsResult18 + DeserializeMember<GUIStyleState>(data, null, "onHover", out value19);
            model.onHover = value19;
            GUIStyleState value20 = model.onNormal;
            fsResult fsResult20 = fsResult19 + DeserializeMember<GUIStyleState>(data, null, "onNormal", out value20);
            model.onNormal = value20;
            RectOffset value21 = model.overflow;
            fsResult fsResult21 = fsResult20 + DeserializeMember<RectOffset>(data, null, "overflow", out value21);
            model.overflow = value21;
            RectOffset value22 = model.padding;
            fsResult fsResult22 = fsResult21 + DeserializeMember<RectOffset>(data, null, "padding", out value22);
            model.padding = value22;
            bool value23 = model.richText;
            fsResult fsResult23 = fsResult22 + DeserializeMember<bool>(data, null, "richText", out value23);
            model.richText = value23;
            bool value24 = model.stretchHeight;
            fsResult fsResult24 = fsResult23 + DeserializeMember<bool>(data, null, "stretchHeight", out value24);
            model.stretchHeight = value24;
            bool value25 = model.stretchWidth;
            fsResult fsResult25 = fsResult24 + DeserializeMember<bool>(data, null, "stretchWidth", out value25);
            model.stretchWidth = value25;
            bool value26 = model.wordWrap;
            fsResult result = fsResult25 + DeserializeMember<bool>(data, null, "wordWrap", out value26);
            model.wordWrap = value26;
            return result;
        }

        public override object CreateInstance(fsData data, Type storageType)
        {
            return (object)new GUIStyle();
        }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static GUIStyle_DirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
