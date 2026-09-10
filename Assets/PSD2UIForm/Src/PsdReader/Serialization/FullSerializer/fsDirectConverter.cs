using System;
using System.Collections.Generic;

namespace cn.efunstudio.psdreader.FullSerializer
{
    public abstract class fsDirectConverter : fsBaseConverter
    {
        private static fsDirectConverter s_ObfuscationSentinel;

        public abstract Type ModelType { get; }

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static fsDirectConverter GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
    public abstract class fsDirectConverter<TModel> : fsDirectConverter
    {
        private static object s_ObfuscationSentinel;

        public override Type ModelType => typeof(TModel);

        public sealed override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
        {
            Dictionary<string, fsData> dictionary = new Dictionary<string, fsData>();
            fsResult result = DoSerialize((TModel)instance, dictionary);
            serialized = new fsData(dictionary);
            return result;
        }

        public sealed override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
        {
            fsResult success = fsResult.Success;
            fsResult fsResult2 = (success += CheckType(data, fsDataType.Object));
            if (!fsResult2.Failed)
            {
                TModel model = (TModel)instance;
                success += DoDeserialize(data.AsDictionary, ref model);
                instance = model;
                return success;
            }
            return success;
        }

        protected abstract fsResult DoSerialize(TModel model, Dictionary<string, fsData> serialized);

        protected abstract fsResult DoDeserialize(Dictionary<string, fsData> data, ref TModel model);

        internal static bool IsObfuscationSentinelNull()
        {
            return s_ObfuscationSentinel == null;
        }

        internal static object GetObfuscationSentinel()
        {
            return s_ObfuscationSentinel;
        }
    }
}
