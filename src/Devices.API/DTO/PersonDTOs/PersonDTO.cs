namespace Devices.API.DTO.PersonDTOs;

public record PersonDTO(
    string PassportNumber,
    string FirstName,
    string MiddleName,
    string LastName,
    string PhoneNumber,
    string Email
);