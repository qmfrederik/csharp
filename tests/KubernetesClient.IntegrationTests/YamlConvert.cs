using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KubernetesClient.IntegrationTests
{
    /// <summary>
    /// Provides helper methods for serializing values to YAML.
    /// </summary>
    public static class YamlConvert
    {
        private static IDeserializer deserializer =
            new DeserializerBuilder()
            .WithNamingConvention(new CamelCaseNamingConvention())
            .Build();

        private static ISerializer serializer =
            new SerializerBuilder()
            .WithNamingConvention(new CamelCaseNamingConvention())
            .Build();

        /// <summary>
        /// Deserializes an object from its YAML representation.
        /// </summary>
        /// <typeparam name="T">
        /// The type of object to deserialize.
        /// </typeparam>
        /// <param name="yaml">
        /// The YAML representation of the object.
        /// </param>
        /// <returns>
        /// A new object.
        /// </returns>
        public static T FromYaml<T>(string yaml)
        {
            return deserializer.Deserialize<T>(yaml);
        }

        /// <summary>
        /// Serializes an object to its YAML representation.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the object to serialize.
        /// </typeparam>
        /// <param name="value">
        /// The object to serialize.
        /// </param>
        /// <returns>
        /// The YAML representation of the object.
        /// </returns>
        public static string Serialize<T>(T value)
        {
            return serializer.Serialize(value);
        }
    }
}
