////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Reflect;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Jaxp.Transform;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Internal.Jaxp.Namespace;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    public abstract class JPConverter
    {
        private static readonly Dictionary<System.Type, JPConverter> converterMap = new Dictionary<System.Type, JPConverter>();

        private static readonly Dictionary<System.Type, Types.ItemType> itemTypeMap = new Dictionary<System.Type, Types.ItemType>();

        private static readonly Dictionary<System.Type, int> cardinalityMap = new Dictionary<System.Type, int>();

        static JPConverter()

        {

            InitConverterMap();

            InitItemTypeMap();

            InitCardinalityMap();

        }

        private static void InitConverterMap()

        {

            converterMap.Put(typeof(XdmValue), new FromXdmValue(AnyItemType.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_MORE));
            converterMap.Put(typeof(XdmItem), new FromXdmValue(AnyItemType.GetInstance(), StaticProperty.ALLOWS_ONE));
            converterMap.Put(typeof(XdmAtomicValue), new FromXdmValue(BuiltInAtomicType.ANY_ATOMIC, StaticProperty.ALLOWS_ONE));
            converterMap.Put(typeof(XdmNode), new FromXdmValue(AnyNodeTest.GetInstance(), StaticProperty.ALLOWS_ONE));
            converterMap.Put(typeof(XdmFunctionItem), new FromXdmValue(AnyFunctionType.GetInstance(), StaticProperty.ALLOWS_ONE));
            converterMap.Put(typeof(XdmMap), new FromXdmValue(MapType.ANY_MAP_TYPE, StaticProperty.ALLOWS_ONE));
            converterMap.Put(typeof(XdmArray), new FromXdmValue(ArrayItemType.GetInstance(), StaticProperty.ALLOWS_ONE));
            converterMap.Put(typeof(XdmEmptySequence), new FromXdmValue(ErrorType.GetInstance(), StaticProperty.ALLOWS_ZERO));
            converterMap.Put(typeof(ISequenceIterator), FromSequenceIterator.INSTANCE);
            converterMap.Put(typeof(ISequence), FromSequence.INSTANCE);
            converterMap.Put(typeof(OneOrMore<>), FromSequence.INSTANCE);
            converterMap.Put(typeof(One<>), FromSequence.INSTANCE);
            converterMap.Put(typeof(ZeroOrOne<>), FromSequence.INSTANCE);
            converterMap.Put(typeof(ZeroOrMore<>), FromSequence.INSTANCE);
            converterMap.Put(typeof(string), FromString.INSTANCE);
            converterMap.Put(typeof(UnicodeString), FromUnicodeString.INSTANCE);
            converterMap.Put(typeof(bool), FromBoolean.INSTANCE);
            converterMap.Put(typeof(bool), FromBoolean.INSTANCE);
            converterMap.Put(typeof(double), FromDouble.INSTANCE);
            converterMap.Put(typeof(double), FromDouble.INSTANCE);
            converterMap.Put(typeof(float), FromFloat.INSTANCE);
            converterMap.Put(typeof(float), FromFloat.INSTANCE);
            converterMap.Put(typeof(BigDecimal), FromBigDecimal.INSTANCE);
            converterMap.Put(typeof(BigInteger), FromBigInteger.INSTANCE);
            converterMap.Put(typeof(long), FromLong.INSTANCE);
            converterMap.Put(typeof(long), FromLong.INSTANCE);
            converterMap.Put(typeof(int), FromInt.INSTANCE);
            converterMap.Put(typeof(int), FromInt.INSTANCE);
            converterMap.Put(typeof(Short), FromShort.INSTANCE);
            converterMap.Put(typeof(short), FromShort.INSTANCE);
            converterMap.Put(typeof(byte), FromByte.INSTANCE);
            converterMap.Put(typeof(byte), FromByte.INSTANCE);
            converterMap.Put(typeof(char), FromCharacter.INSTANCE);
            converterMap.Put(typeof(URI), FromURI.INSTANCE);
            converterMap.Put(typeof(URL), FromURI.INSTANCE);
            converterMap.Put(typeof(Date), FromDate.INSTANCE);
            converterMap.Put(typeof(long[]), FromLongArray.INSTANCE);
            converterMap.Put(typeof(int[]), FromIntArray.INSTANCE);
            converterMap.Put(typeof(short[]), FromShortArray.INSTANCE);
            converterMap.Put(typeof(byte[]), FromByteArray.INSTANCE);
            converterMap.Put(typeof(char[]), FromCharArray.INSTANCE);
            converterMap.Put(typeof(double[]), FromDoubleArray.INSTANCE);
            converterMap.Put(typeof(float[]), FromFloatArray.INSTANCE);
            converterMap.Put(typeof(bool[]), FromBooleanArray.INSTANCE);
            converterMap.Put(typeof(Collection<>), FromCollection.INSTANCE);
        }

        private static void InitItemTypeMap()

        {

            itemTypeMap.Put(typeof(BooleanValue), BuiltInAtomicType.BOOLEAN);
            itemTypeMap.Put(typeof(StringValue), BuiltInAtomicType.STRING);
            itemTypeMap.Put(typeof(DoubleValue), BuiltInAtomicType.DOUBLE);
            itemTypeMap.Put(typeof(FloatValue), BuiltInAtomicType.FLOAT);
            itemTypeMap.Put(typeof(BigDecimalValue), BuiltInAtomicType.DECIMAL);
            itemTypeMap.Put(typeof(IntegerValue), BuiltInAtomicType.INTEGER);
            itemTypeMap.Put(typeof(DurationValue), BuiltInAtomicType.DURATION);
            itemTypeMap.Put(typeof(DayTimeDurationValue), BuiltInAtomicType.DAY_TIME_DURATION);
            itemTypeMap.Put(typeof(YearMonthDurationValue), BuiltInAtomicType.YEAR_MONTH_DURATION);
            itemTypeMap.Put(typeof(DateTimeValue), BuiltInAtomicType.DATE_TIME);
            itemTypeMap.Put(typeof(DateValue), BuiltInAtomicType.DATE);
            itemTypeMap.Put(typeof(TimeValue), BuiltInAtomicType.TIME);
            itemTypeMap.Put(typeof(GYearValue), BuiltInAtomicType.G_YEAR);
            itemTypeMap.Put(typeof(GYearMonthValue), BuiltInAtomicType.G_YEAR_MONTH);
            itemTypeMap.Put(typeof(GMonthValue), BuiltInAtomicType.G_MONTH);
            itemTypeMap.Put(typeof(GMonthDayValue), BuiltInAtomicType.G_MONTH_DAY);
            itemTypeMap.Put(typeof(GDayValue), BuiltInAtomicType.G_DAY);
            itemTypeMap.Put(typeof(AnyURIValue), BuiltInAtomicType.ANY_URI);
            itemTypeMap.Put(typeof(QNameValue), BuiltInAtomicType.QNAME);
            itemTypeMap.Put(typeof(NotationValue), BuiltInAtomicType.NOTATION);
            itemTypeMap.Put(typeof(HexBinaryValue), BuiltInAtomicType.HEX_BINARY);
            itemTypeMap.Put(typeof(Base64BinaryValue), BuiltInAtomicType.BASE64_BINARY);
            itemTypeMap.Put(typeof(NodeInfo), AnyNodeTest.GetInstance());
            itemTypeMap.Put(typeof(ITreeInfo), NodeKindTest.DOCUMENT);
            itemTypeMap.Put(typeof(MapItem), MapType.GetInstance());
            itemTypeMap.Put(typeof(ArrayItem), ArrayItemType.GetInstance());
            itemTypeMap.Put(typeof(IFunctionItem), AnyFunctionType.GetInstance());
            itemTypeMap.Put(typeof(AtomicValue), BuiltInAtomicType.ANY_ATOMIC); //itemTypeMap.put(UntypedAtomicValue.class, BuiltInAtomicType.UNTYPED_ATOMIC);
        }

        private static void InitCardinalityMap()

        {
            cardinalityMap.Put(typeof(ISequence), StaticProperty.ALLOWS_ZERO_OR_MORE);
            cardinalityMap.Put(typeof(ZeroOrMore<>), StaticProperty.ALLOWS_ZERO_OR_MORE);
            cardinalityMap.Put(typeof(OneOrMore<>), StaticProperty.ALLOWS_ONE_OR_MORE);
            cardinalityMap.Put(typeof(One<>), StaticProperty.EXACTLY_ONE);
            cardinalityMap.Put(typeof(ZeroOrOne<>), StaticProperty.ALLOWS_ZERO_OR_ONE);
            cardinalityMap.Put(typeof(XdmValue), StaticProperty.ALLOWS_ZERO_OR_MORE);
            cardinalityMap.Put(typeof(XdmItem), StaticProperty.ALLOWS_ZERO_OR_MORE);
            cardinalityMap.Put(typeof(XdmEmptySequence), StaticProperty.ALLOWS_ZERO);
        }

        public static JPConverter Allocate(System.Type javaClass, OutSmart.DAXon.Internal.Reflect.Type genericType, Configuration config)
        {
            if (typeof(OutSmart.DAXon.Internal.Jaxp.Namespace.QName).IsAssignableFrom(javaClass))
            {
                return FromQName.INSTANCE;
            }

            if (typeof(ISequence).IsAssignableFrom(javaClass))
            {

                // Following code caters for classes such as OneOrMore<BooleanValue>
                if (genericType is ParameterizedType)
                {
                    OutSmart.DAXon.Internal.Reflect.Type[] @params = (OutSmart.DAXon.Internal.Reflect.Type[])((ParameterizedType)genericType).ActualTypeArguments;
                    if (@params.Length == 1 && @params[0] is System.Type && typeof(IItem).IsAssignableFrom((System.Type)@params[0]))
                    {
                        Types.ItemType itemType = itemTypeMap.Get((System.Type)(@params[0]));
                        int cardinality = cardinalityMap.Get(javaClass);
                        if (itemType != null && cardinality != null)
                        {
                            return new FromSequence(itemType, cardinality);
                        }
                    }
                }
                else
                {
                    Types.ItemType itemType = itemTypeMap.Get(javaClass);
                    if (itemType != null)
                    {
                        return new FromSequence(itemType, StaticProperty.ALLOWS_ZERO_OR_ONE);
                    }
                }
            }

            JPConverter c = converterMap.Get(javaClass);
            if (c != null)
            {
                return c;
            }

            if (javaClass.Equals(typeof(object)))
            {
                return FromObject.INSTANCE;
            }

            if (typeof(NodeInfo).IsAssignableFrom(javaClass))
            {

                // probably now redundant
                return new FromSequence(AnyNodeTest.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_ONE);
            }

            if (typeof(ResolvedResource).IsAssignableFrom(javaClass))
            {
                return FromSource.INSTANCE;
            }

            foreach (KeyValuePair<System.Type, JPConverter> e in converterMap.EntrySet())
            {
                if (e.Key.IsAssignableFrom(javaClass))
                {
                    return e.Value;
                }
            }

            IList<IExternalObjectModel> externalObjectModels = config.ExternalObjectModels;
            foreach (IExternalObjectModel model in externalObjectModels)
            {
                try
                {
                    JPConverter converter = model.GetJPConverter(javaClass, config);
                    if (converter != null)
                    {
                        return converter;
                    }
                }
                catch (Exception e)
                {
                    config.DeregisterExternalObjectModel(model);
                }
            }

            if (javaClass.IsArray)
            {
                System.Type itemClass = javaClass.GetElementType();
                return new FromObjectArray(Allocate(itemClass, null, config));
            }

            if (javaClass.Equals(typeof(void)))
            {
                return VoidConverter.INSTANCE;
            }

            JavaExternalObjectType result;
            lock (config)
            {
                result = JavaExternalObjectType.Of(javaClass);
            }

            return new ExternalObjectWrapper(result);
        }

        public abstract IGroundedValue Convert(object @object, IXPathContext context);
        public abstract Types.ItemType GetItemType();
        public virtual int GetCardinality()
        {

            // default implementation
            return StaticProperty.EXACTLY_ONE;
        }

        public class FromObject : JPConverter
        {
            public static readonly FromObject INSTANCE = new FromObject();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                System.Type theClass = @object.GetType();
                JPConverter instanceConverter = Allocate(theClass, null, context.GetConfiguration());
                if (instanceConverter is FromObject)
                {
                    JavaExternalObjectType result;
                    lock (context.GetConfiguration())
                    {
                        result = JavaExternalObjectType.Of(theClass);
                    }

                    instanceConverter = new ExternalObjectWrapper(result);
                }

                return instanceConverter.Convert(@object, context);
            }

            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromSequenceIterator : JPConverter
        {
            public static readonly FromSequenceIterator INSTANCE = new FromSequenceIterator();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                try
                {
                    return SequenceTool.ToGroundedValue(((ISequenceIterator)@object));
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }

            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromXdmValue : JPConverter
        {
            private readonly Types.ItemType resultType;
            private readonly int cardinality;
            public FromXdmValue(Types.ItemType resultType, int cardinality)
            {
                this.resultType = resultType;
                this.cardinality = cardinality;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return (IGroundedValue)((XdmValue)@object).UnderlyingValue;
            }

            public override Types.ItemType GetItemType()
            {
                return resultType;
            }

            public override int GetCardinality()
            {
                return cardinality;
            }
        }

        public class FromSequence : JPConverter
        {
            public static readonly FromSequence INSTANCE = new FromSequence(AnyItemType.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_MORE);
            private readonly Types.ItemType resultType;
            private readonly int cardinality;
            public FromSequence(Types.ItemType resultType, int cardinality)
            {
                this.resultType = resultType;
                this.cardinality = cardinality;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return ((ISequence)@object).Materialize();
            }

            public override Types.ItemType GetItemType()
            {
                return resultType;
            }

            public override int GetCardinality()
            {
                return cardinality;
            }
        }

        public class FromString : JPConverter
        {
            public static readonly FromString INSTANCE = new FromString();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new StringValue((string)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        public class FromUnicodeString : JPConverter
        {
            public static readonly FromUnicodeString INSTANCE = new FromUnicodeString();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new StringValue((UnicodeString)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        public class FromBoolean : JPConverter
        {
            public static readonly FromBoolean INSTANCE = new FromBoolean();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return BooleanValue.Get((bool)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.BOOLEAN;
            }
        }

        public class FromDouble : JPConverter
        {
            public static readonly FromDouble INSTANCE = new FromDouble();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new DoubleValue((double)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DOUBLE;
            }
        }

        public class FromFloat : JPConverter
        {
            public static readonly FromFloat INSTANCE = new FromFloat();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new FloatValue((float)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.FLOAT;
            }
        }

        public class FromBigDecimal : JPConverter
        {
            public static readonly FromBigDecimal INSTANCE = new FromBigDecimal();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new BigDecimalValue((BigDecimal)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DECIMAL;
            }
        }

        public class FromBigInteger : JPConverter
        {
            public static readonly FromBigInteger INSTANCE = new FromBigInteger();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return IntegerValue.MakeIntegerValue((BigInteger)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        public class FromLong : JPConverter
        {
            public static readonly FromLong INSTANCE = new FromLong();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value((long)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        public class FromInt : JPConverter
        {
            public static readonly FromInt INSTANCE = new FromInt();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value((int)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        public class FromShort : JPConverter
        {
            public static readonly FromShort INSTANCE = new FromShort();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value(((Short)@object).IntValue());
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        public class FromByte : JPConverter
        {
            public static readonly FromByte INSTANCE = new FromByte();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new Int64Value(((byte)@object).IntValue());
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INTEGER;
            }
        }

        public class FromCharacter : JPConverter
        {
            public static readonly FromCharacter INSTANCE = new FromCharacter();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new StringValue(@object.ToString());
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        public class FromQName : JPConverter
        {
            public static readonly FromQName INSTANCE = new FromQName();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                OutSmart.DAXon.Internal.Jaxp.Namespace.QName qn = (OutSmart.DAXon.Internal.Jaxp.Namespace.QName)@object;
                return new QNameValue(qn.GetPrefix(), NamespaceUri.Of(qn.GetNamespaceURI()), qn.GetLocalPart());
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.QNAME;
            }
        }

        public class FromURI : JPConverter
        {
            public static readonly FromURI INSTANCE = new FromURI();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return new AnyURIValue((@object.ToString()));
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.ANY_URI;
            }
        }

        public class FromDate : JPConverter
        {
            public static readonly FromDate INSTANCE = new FromDate();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return DateTimeValue.FromJavaDate((Date)@object);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DATE_TIME;
            }
        }

        public class ExternalObjectWrapper : JPConverter
        {
            private readonly JavaExternalObjectType resultType;
            public ExternalObjectWrapper(JavaExternalObjectType resultType)
            {
                this.resultType = resultType;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                if (@object == null)
                {
                    return null;
                }
                else if (resultType.JavaClass.IsInstance(@object))
                {
                    return new ObjectValue<object>(@object, resultType.JavaClass);
                }
                else
                {
                    throw new XPathException("Java external object of type " + @object.GetType().GetName() + " is not an instance of the required type " + resultType.JavaClass.GetName(), "XPTY0004");
                }
            }

            public override Types.ItemType GetItemType()
            {
                return resultType;
            }
        }

        public class VoidConverter : JPConverter
        {
            public static readonly VoidConverter INSTANCE = new VoidConverter();
            public VoidConverter()
            {
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return EmptySequence.GetInstance();
            }

            /// <summary>
            /// Deliberately avoid giving type information
            /// </summary>
            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }
        }

        public class FromCollection : JPConverter
        {
            public static readonly FromCollection INSTANCE = new FromCollection();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IList<IItem> list = new List<IItem>(((Collection<object>)@object).Count);
                int a = 0;
                foreach (object obj in (Collection<object>)@object)
                {
                    JPConverter itemConverter = Allocate(obj.GetType(), null, context.GetConfiguration());
                    try
                    {
                        IItem item = SequenceTool.AsItem(itemConverter.Convert(obj, context));
                        if (item != null)
                        {
                            list.Add(item);
                        }
                    }
                    catch (XPathException e)
                    {
                        throw new XPathException("Returned Collection contains an object that cannot be converted to an Item (" + obj.GetType() + "): " + e.GetMessage(), DAXonErrorCode.SXJE0051);
                    }
                }

                return new SequenceExtent.Of<IItem>(list);
            }

            public override Types.ItemType GetItemType()
            {
                return AnyItemType.GetInstance();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromSource : JPConverter
        {
            public static readonly FromSource INSTANCE = new FromSource();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                ParseOptions options = new ParseOptions();
                Controller controller = context.GetController();
                if (controller != null)
                {
                    options = options.WithSchemaValidationMode(controller.SchemaValidationMode);
                }

                if (@object is ITreeInfo)
                {
                    return ((ITreeInfo)@object).GetRootNode();
                }

                return context.GetConfiguration().BuildDocumentTree((ResolvedResource)@object, options).GetRootNode();
            }

            public override Types.ItemType GetItemType()
            {
                return AnyNodeTest.GetInstance();
            }
        }

        public class FromLongArray : JPConverter
        {
            public static readonly FromLongArray INSTANCE = new FromLongArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((long[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(((long[])@object)[i], BuiltInAtomicType.LONG);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.LONG;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromIntArray : JPConverter
        {
            public static readonly FromIntArray INSTANCE = new FromIntArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((int[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(((int[])@object)[i], BuiltInAtomicType.INT);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.INT;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromShortArray : JPConverter
        {
            public static readonly FromShortArray INSTANCE = new FromShortArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((short[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(((short[])@object)[i], BuiltInAtomicType.SHORT);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.SHORT;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromByteArray : JPConverter
        {
            public static readonly FromByteArray INSTANCE = new FromByteArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((byte[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = Int64Value.MakeDerived(255 & (int)((byte[])@object)[i], BuiltInAtomicType.UNSIGNED_BYTE);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.UNSIGNED_BYTE;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromCharArray : JPConverter
        {
            public static readonly FromCharArray INSTANCE = new FromCharArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                return StringValue.MakeStringValue(new string((char[])@object));
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.STRING;
            }
        }

        public class FromDoubleArray : JPConverter
        {
            public static readonly FromDoubleArray INSTANCE = new FromDoubleArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((double[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = new DoubleValue(((double[])@object)[i]);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.DOUBLE;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromFloatArray : JPConverter
        {
            public static readonly FromFloatArray INSTANCE = new FromFloatArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((float[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = new DoubleValue(((float[])@object)[i]);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.FLOAT;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromBooleanArray : JPConverter
        {
            public static readonly FromBooleanArray INSTANCE = new FromBooleanArray();
            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                IItem[] array = new IItem[((bool[])@object).Length];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = BooleanValue.Get(((bool[])@object)[i]);
                }

                return new SequenceExtent.Of<IItem>(array);
            }

            public override Types.ItemType GetItemType()
            {
                return BuiltInAtomicType.BOOLEAN;
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public class FromObjectArray : JPConverter
        {
            private readonly JPConverter itemConverter;
            public FromObjectArray(JPConverter itemConverter)
            {
                this.itemConverter = itemConverter;
            }

            public override IGroundedValue Convert(object @object, IXPathContext context)
            {
                object[] arrayObject = (Object[])@object;
                IList<IItem> newArray = new List<IItem>(arrayObject.Length);
                int a = 0;
                foreach (object member in arrayObject)
                {
                    if (member != null)
                    {
                        try
                        {
                            IItem newItem = SequenceTool.AsItem(itemConverter.Convert(member, context));
                            if (newItem != null)
                            {
                                newArray.Add(newItem);
                            }
                        }
                        catch (XPathException e)
                        {
                            throw new XPathException("Returned array contains an object that cannot be converted to an Item (" + member.GetType() + "): " + e.GetMessage(), DAXonErrorCode.SXJE0051);
                        }
                    }
                    else
                    {
                        throw new XPathException("Returned array contains null values: cannot convert to items", DAXonErrorCode.SXJE0051);
                    }
                }

                return new SequenceExtent.Of<IItem>(newArray);
            }

            public override Types.ItemType GetItemType()
            {
                return itemConverter.GetItemType();
            }

            public override int GetCardinality()
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }
    }
}
