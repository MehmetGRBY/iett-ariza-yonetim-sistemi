using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IettFaultManagement.Api.Models.Database;

[Keyless]
public partial class VwUnreadNotificationCount
{
    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("personnel_number")]
    [StringLength(30)]
    public string? PersonnelNumber { get; set; }

    [Column("first_name")]
    [StringLength(100)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [StringLength(100)]
    public string? LastName { get; set; }

    [Column("unread_notification_count")]
    public long? UnreadNotificationCount { get; set; }

    [Column("latest_unread_notification_at")]
    public DateTime? LatestUnreadNotificationAt { get; set; }
}
