using System;
using System.Collections;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace VBart.EventsDemo.Inventory.Aggregator.Domain
{
    public static class FieldMasks
    {
        public static FieldMask FromChanges<T>(IMessage<T> original, IMessage<T> modified)
            where T : class, IMessage<T>
        {
            if (modified == null)
            {
                throw new ArgumentNullException(nameof(modified));
            }

            var mask = new FieldMask();
            CompareField(mask, string.Empty, original, modified);
            return mask;
        }

        private static void CompareField(FieldMask mask, string fieldName, IMessage original, IMessage modified)
        {
            var descriptor = modified.Descriptor;
            foreach (var childField in descriptor.Fields.InFieldNumberOrder())
            {
                var childFieldName = GetFullFieldName(fieldName, childField);
                var originalChildValue = childField.Accessor.GetValue(original);
                var modifiedChildValue = childField.Accessor.GetValue(modified);
                if (childField.IsRepeated)
                {
                    if (!Equals(originalChildValue, modifiedChildValue))
                    {
                        mask.Paths.Add(childFieldName);
                    }
                }
                else
                {
                    switch (childField.FieldType)
                    {
                        case FieldType.Message:
                            if (!Equals(originalChildValue, modifiedChildValue))
                            {
                                // For wrapper types, just emit the field name.
                                if (IsWrapperType(childField.MessageType))
                                {
                                    mask.Paths.Add(childFieldName);
                                }
                                else if (originalChildValue == null)
                                {
                                    // New value? Emit the field names for all known fields in the message,
                                    // recurring for nested messages.
                                    AddNewFields(mask, childFieldName, (IMessage)modifiedChildValue);
                                }
                                else if (modifiedChildValue == null)
                                {
                                    // Just emit the deleted field name
                                    mask.Paths.Add(childFieldName);
                                }
                                else
                                {
                                    CompareField(
                                        mask,
                                        childFieldName,
                                        (IMessage)originalChildValue,
                                        (IMessage)modifiedChildValue);
                                }
                            }

                            break;

                        case FieldType.Group:
                            throw new NotSupportedException("Groups are not supported in proto3");
                        default: // Primitives
                            if (childField.HasPresence)
                            {
                                // Presence fields should be handled based on whether the field has
                                // value.
                                var originalChildHasValue = childField.Accessor.HasValue(original);
                                var modifiedChildHasValue = childField.Accessor.HasValue(modified);

                                // Both fields have value, but the values don't match.
                                if (originalChildHasValue && modifiedChildHasValue &&
                                    !Equals(originalChildValue, modifiedChildValue))
                                {
                                    mask.Paths.Add(childFieldName);
                                }
                                else if (originalChildHasValue || modifiedChildHasValue)
                                {
                                    // Only one of the fields have value.
                                    mask.Paths.Add(childFieldName);
                                }
                            }
                            else if (!Equals(originalChildValue, modifiedChildValue))
                            {
                                mask.Paths.Add(childFieldName);
                            }

                            break;
                    }
                }
            }
        }

        private static void AddNewFields(FieldMask mask, string fieldName, IMessage message)
        {
            var descriptor = message.Descriptor;
            foreach (var field in descriptor.Fields.InFieldNumberOrder())
            {
                var name = GetFullFieldName(fieldName, field);

                // For single message fields, recurse if there's a value; otherwise just add the
                // field name.
                var value = field.Accessor.GetValue(message);
                if (!field.IsRepeated && field.FieldType == FieldType.Message && value is IMessage)
                {
                    if (value is IMessage o)
                    {
                        if (o != null)
                        {
                            AddNewFields(mask, name, o);
                        }
                        else
                        {
                            mask.Paths.Add(name);
                        }
                    }
                }
                else if (field.HasPresence)
                {
                    // The presence fields should first be checked for HasValue.
                    if (field.Accessor.HasValue(message))
                    {
                        mask.Paths.Add(name);
                    }
                }
                else if (value != null)
                {
                    if (field.IsRepeated)
                    {
                        if (value is IList list && list.Count is > 0)
                        {
                            // Generate fieldmask for a repeated field only if there's at least one
                            // element in the list. Also, don't recurse, since fieldmask doesn't
                            // support index notation.
                            mask.Paths.Add(name);
                        }
                    }
                    else
                    {
                        // The value could be nullable.
                        mask.Paths.Add(name);
                    }
                }
            }
        }

        private static string GetFullFieldName(string fieldName, FieldDescriptor childField) =>
            fieldName?.Length == 0 ? childField.Name : $"{fieldName}.{childField.Name}";

        private static bool IsWrapperType(MessageDescriptor descriptor) =>
            descriptor.File.Package == "google.protobuf" &&
            descriptor.File.Name == "google/protobuf/wrappers.proto";
    }
}