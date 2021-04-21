using System;
using AutoMapper;

namespace VBart.EventsDemo.Inventory.Api.AutoMapper
{
    public class GrpcGuidValueConverter : IValueConverter<string?, Guid>
    {
        public Guid Convert(string? sourceMember,
            ResolutionContext context)
        {
            if (string.IsNullOrWhiteSpace(sourceMember))
            {
                return Guid.Empty;
            }

            if (!Guid.TryParse(sourceMember, out var result))
            {
                throw new ArgumentException("Argument is not a Guid.", nameof(sourceMember));
            }

            return result;
        }
    }
}