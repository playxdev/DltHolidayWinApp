using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dlt.Holiday.Sync.Models
{
    public class HolidayDto
    {
        [JsonProperty("holiday_id")]
        public long HolidayId { get; set; }

        [JsonProperty("holiday_name")]
        public string HolidayName { get; set; }

        [JsonProperty("holiday_date")]
        public string HolidayDate { get; set; }

        [JsonProperty("create_by")]
        public string CreateBy { get; set; }

        [JsonProperty("create_date")]
        public string CreateDate { get; set; }

        [JsonProperty("update_by")]
        public string UpdateBy { get; set; }

        [JsonProperty("update_date")]
        public string UpdateDate { get; set; }

        [JsonProperty("active_status")]
        public int ActiveStatus { get; set; }
    }

    public class PaginatedApiResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("data")]
        public List<T> Data { get; set; }

        [JsonProperty("total")]
        public int Total { get; set; }

        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class LoginResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }
}
