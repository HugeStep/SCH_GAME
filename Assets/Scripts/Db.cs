using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;

// 로그인한 학생 정보 (씬이 바뀌어도 유지)
public static class Session
{
    public static string Dept, Id, Name;
    public static long Best;
    public static bool LoggedIn => !string.IsNullOrEmpty(Id);
    public static void Clear() { Dept = Id = Name = null; Best = 0; }
}

// 화면에 보여줄 메시지가 있는 오류
public class UserError : Exception { public UserError(string msg) : base(msg) { } }

// Firestore 저장소. 문서: users/{학번} = { dept, studentId, name, bestScore, bestMeters, bestKills, bestHits, lastScore, plays }
public static class Db
{
    public struct Entry { public string Dept, Name, Id; public long Score; }

    static FirebaseFirestore fs;

    static async Task<CollectionReference> Users()
    {
        if (fs == null)
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available) throw new UserError("Firebase를 사용할 수 없습니다: " + status);
            fs = FirebaseFirestore.DefaultInstance;
        }
        return fs.Collection("users");
    }

    // 학번이 처음이면 새로 등록, 이미 있으면 이름이 같은지 확인한다.
    public static async Task Login(string dept, string id, string name)
    {
        var doc = (await Users()).Document(id);
        var snap = await doc.GetSnapshotAsync();
        long best = 0;
        if (snap.Exists)
        {
            if (!snap.TryGetValue<string>("name", out var saved) || saved != name)
                throw new UserError("이미 다른 이름으로 등록된 학번입니다. 학번과 이름을 확인해 주세요.");
            snap.TryGetValue("bestScore", out best);
            if (!snap.TryGetValue<string>("dept", out var savedDept) || savedDept != dept)
                await doc.UpdateAsync("dept", dept);
        }
        else
        {
            await doc.SetAsync(new Dictionary<string, object>
            {
                { "dept", dept }, { "studentId", id }, { "name", name },
                { "bestScore", 0L }, { "plays", 0L }, { "createdAt", FieldValue.ServerTimestamp },
            });
        }
        Session.Dept = dept;
        Session.Id = id;
        Session.Name = name;
        Session.Best = best;
    }

    // 결과 저장. 최고 기록을 넘었으면 true.
    public static async Task<bool> SaveResult(int score, int meters, int kills, int hits)
    {
        var data = new Dictionary<string, object>
        {
            { "plays", FieldValue.Increment(1L) }, { "lastScore", score }, { "updatedAt", FieldValue.ServerTimestamp },
        };
        bool best = score > Session.Best;
        if (best)
        {
            data["bestScore"] = score;
            data["bestMeters"] = meters;
            data["bestKills"] = kills;
            data["bestHits"] = hits;
            Session.Best = score;
        }
        await (await Users()).Document(Session.Id).SetAsync(data, SetOptions.MergeAll);
        return best;
    }

    // 내 최고 점수 기준 전체 순위 (동점은 같은 순위). 기록이 없으면 0.
    public static async Task<long> MyRank()
    {
        if (Session.Best <= 0) return 0;
        var snap = await (await Users()).WhereGreaterThan("bestScore", Session.Best).Count.GetSnapshotAsync(AggregateSource.Server);
        return snap.Count + 1;
    }

    // 랭킹 한 페이지. Start = 첫 줄의 위치(0부터), FirstRank = 첫 줄의 순위(동점 반영).
    // 앞뒤 페이지는 이 페이지 양 끝 문서에서 이어 읽어서, 페이지마다 n개만 읽는다.
    public class Page
    {
        public List<Entry> List = new();
        public long Start, FirstRank;
        internal DocumentSnapshot first, last;
    }

    static Query Ranked(CollectionReference users) => users.WhereGreaterThan("bestScore", 0).OrderByDescending("bestScore");

    // 기록이 있는 전체 인원
    public static async Task<long> Count() =>
        (await (await Users()).WhereGreaterThan("bestScore", 0).Count.GetSnapshotAsync(AggregateSource.Server)).Count;

    public static async Task<Page> First(int n) => await Read(Ranked(await Users()).Limit(n), 0);
    // score 점(동점 중 맨 앞)부터. index = 그 줄의 위치 (= 내 순위 - 1)
    public static async Task<Page> At(long score, long index, int n) => await Read(Ranked(await Users()).StartAt(score).Limit(n), index);
    public static async Task<Page> Next(Page p, int n) => await Read(Ranked(await Users()).StartAfter(p.last).Limit(n), p.Start + p.List.Count);
    public static async Task<Page> Prev(Page p, int n)
    {
        var page = await Read(Ranked(await Users()).EndBefore(p.first).LimitToLast(n), 0);
        page.Start = p.Start - page.List.Count;
        return page;
    }

    static async Task<Page> Read(Query q, long start)
    {
        // 서버에서만 읽는다: 기본값은 연결이 안 되면 빈 캐시를 돌려줘서 "기록 없음"으로 잘못 보인다
        var snap = await q.GetSnapshotAsync(Source.Server);
        var page = new Page { Start = start };
        foreach (var d in snap.Documents)
        {
            d.TryGetValue<string>("dept", out var dept);
            d.TryGetValue<string>("name", out var name);
            d.TryGetValue<long>("bestScore", out var score);
            page.List.Add(new Entry { Dept = dept, Name = name, Id = d.Id, Score = score });
            page.first ??= d;
            page.last = d;
        }
        if (page.List.Count > 0) // 앞 페이지와 동점일 수 있어서 첫 줄 순위는 따로 센다
            page.FirstRank = (await (await Users()).WhereGreaterThan("bestScore", page.List[0].Score).Count.GetSnapshotAsync(AggregateSource.Server)).Count + 1;
        return page;
    }

    // 랭킹 화면용 이름 가리기: 홍길동 → 홍*동
    public static string Mask(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 2) return name;
        if (name.Length == 2) return name[0] + "*";
        return name[0] + new string('*', name.Length - 2) + name[^1];
    }

    // 사용자에게 보여줄 오류 문구
    public static string Friendly(Exception e)
    {
        while (e is AggregateException && e.InnerException != null) e = e.InnerException;
        if (e is UserError) return e.Message;
        var m = e.Message ?? "";
        if (m.Contains("PERMISSION_DENIED") || m.Contains("permission")) return "DB 접근 권한이 없습니다. (Firestore 보안 규칙 확인 필요)";
        if (m.Contains("does not exist")) return "Firestore 데이터베이스가 아직 만들어지지 않았습니다.";
        if (m.Contains("offline") || m.Contains("from server") || m.Contains("UNAVAILABLE")) return "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.";
        return "오류: " + m;
    }
}
