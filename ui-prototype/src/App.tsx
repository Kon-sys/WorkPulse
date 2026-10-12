import { useState, type ReactNode } from "react";
import { AnimatePresence, motion, MotionConfig } from "motion/react";
import * as Dialog from "@radix-ui/react-dialog";
import {
  IconActivity,
  IconSearch,
  IconBell,
  IconChevronDown,
  IconCalendar,
  IconArrowUpRight,
  IconArrowRight,
  IconUsers,
  IconClock,
  IconAlertCircle,
  IconX,
  IconCheck,
  IconPlus,
  IconDownload,
  IconLogout,
  IconAdjustments,
  IconWifiOff,
  IconChartBar,
  IconFileText,
} from "@tabler/icons-react";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
import {
  people,
  initialEvents,
  initialPolicies,
  chartData,
  type Person,
  type EventItem,
} from "./data";

const tabs = [
  "Обзор",
  "Сотрудники",
  "Нарушения",
  "Политики",
  "Отчёты",
  "Настройки",
];
const roles = ["HR-менеджер", "Руководитель", "Администратор"];
function Avatar({ person }: { person: Person }) {
  return <span className={"avatar " + person.color}>{person.initials}</span>;
}
function Pill({ children, kind = "" }: { children: ReactNode; kind?: string }) {
  return <span className={"pill " + kind}>{children}</span>;
}
function Modal({
  open,
  onClose,
  title,
  children,
  drawer = false,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  drawer?: boolean;
}) {
  return (
    <Dialog.Root open={open} onOpenChange={(v) => !v && onClose()}>
      <Dialog.Portal>
        <Dialog.Overlay className="overlay" />
        <Dialog.Content className={drawer ? "dialog drawer" : "dialog"}>
          <div className="dialog-head">
            <Dialog.Title>{title}</Dialog.Title>
            <Dialog.Close className="icon-button" aria-label="Закрыть">
              <IconX size={19} />
            </Dialog.Close>
          </div>
          <Dialog.Description className="sr-only">
            Демонстрационные данные WorkPulse. Изменения действуют в текущей
            сессии.
          </Dialog.Description>
          {children}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
function Timeline({ index = 0 }: { index?: number }) {
  const lengths = [
    [22, 2, 15, 7, 15],
    [18, 5, 20, 4, 14],
    [20, 7, 12, 5, 17],
    [24, 3, 18, 4, 12],
    [15, 5, 14, 12, 15],
    [18, 4, 12, 7, 13],
  ][index % 6];
  return (
    <div
      className="timeline"
      aria-label="Активность с 09:00 до 14:30, далее рабочий день ещё не завершён"
    >
      {lengths.map((width, i) => (
        <motion.span
          key={i}
          className={
            i === 1 || i === 3
              ? index === 4 && i === 3
                ? "segment coral"
                : "segment yellow"
              : "segment green"
          }
          style={{ width: width + "%" }}
          initial={{ scaleX: 0 }}
          animate={{ scaleX: 1 }}
          transition={{ duration: 0.55, delay: index * 0.035 + i * 0.045 }}
        />
      ))}
      <span className="future" />
      <span className="now-line" />
    </div>
  );
}

export function App() {
  const [page, setPage] = useState("Обзор"),
    [role, setRole] = useState(roles[0]),
    [login, setLogin] = useState(false),
    [profile, setProfile] = useState(false),
    [notification, setNotification] = useState(false),
    [searchOpen, setSearchOpen] = useState(false),
    [search, setSearch] = useState(""),
    [department, setDepartment] = useState("Все отделы"),
    [selected, setSelected] = useState<Person | null>(null),
    [events, setEvents] = useState(initialEvents),
    [event, setEvent] = useState<EventItem | null>(null),
    [eventFilter, setEventFilter] = useState("Все события"),
    [comment, setComment] = useState(""),
    [comments, setComments] = useState<Record<number, string>>({}),
    [policies, setPolicies] = useState(initialPolicies),
    [policy, setPolicy] = useState<(typeof initialPolicies)[number] | null>(
      null,
    ),
    [report, setReport] = useState(false),
    [period, setPeriod] = useState("Неделя"),
    [date, setDate] = useState("2026-10-12"),
    [toast, setToast] = useState(""),
    [settings, setSettings] = useState({ notify: true, motion: true }),
    [name, setName] = useState("Елена Козлова");
  const notify = (text: string) => {
    setToast(text);
    window.setTimeout(() => setToast(""), 3500);
  };
  const navigate = (next: string) => {
    setPage(next);
    setSearch("");
    setDepartment("Все отделы");
    setProfile(false);
    setNotification(false);
  };
  const newEvents = events.filter((e) => e.status === "Новое");
  const filtered = people.filter(
    (p) =>
      (department === "Все отделы" || p.department === department) &&
      `${p.name} ${p.job}`.toLowerCase().includes(search.toLowerCase()),
  );
  const exportCSV = () => {
    const csv =
      "\uFEFFСотрудник;Отдел;Активность;Рабочее время\n" +
      people
        .map((p) => `${p.name};${p.department};${p.score}%;${p.time}`)
        .join("\n");
    const link = document.createElement("a");
    link.href = URL.createObjectURL(
      new Blob([csv], { type: "text/csv;charset=utf-8" }),
    );
    link.download = "WorkPulse-demo-report.csv";
    link.click();
    URL.revokeObjectURL(link.href);
    notify("CSV с демоданными скачан");
  };
  const resolveEvent = () => {
    if (event && comment.trim())
      setComments({ ...comments, [event.id]: comment.trim() });
    if (event)
      setEvents(
        events.map((e) =>
          e.id === event.id ? { ...e, status: "Рассмотрено" } : e,
        ),
      );
    setEvent(null);
    setComment("");
    notify("Событие рассмотрено");
  };
  const Chart = () => (
    <div
      className="chart"
      role="img"
      aria-label={
        period === "Неделя"
          ? "Активность: понедельник 82%, вторник 89%, среда 85%, четверг 92%, пятница 86%"
          : "Активность: понедельник 78%, вторник 85%, среда 81%, четверг 88%, пятница 82%"
      }
    >
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart
          data={chartData.map((d) => ({
            ...d,
            productive: d.productive + (period === "Месяц" ? -4 : 0),
          }))}
          margin={{ top: 12, right: 12, left: -20, bottom: 0 }}
        >
          <defs>
            <linearGradient id="areaFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#3f9478" stopOpacity={0.25} />
              <stop offset="100%" stopColor="#3f9478" stopOpacity={0} />
            </linearGradient>
          </defs>
          <CartesianGrid stroke="#edf0eb" vertical={false} />
          <XAxis
            dataKey="day"
            axisLine={false}
            tickLine={false}
            tick={{ fill: "#78817b", fontSize: 12 }}
          />
          <YAxis
            domain={[0, 100]}
            axisLine={false}
            tickLine={false}
            tick={{ fill: "#78817b", fontSize: 12 }}
            unit="%"
          />
          <Tooltip
            formatter={(v) => [`${v}%`, "Активность"]}
            contentStyle={{
              borderRadius: 12,
              borderColor: "#e3e8e1",
              fontSize: 12,
            }}
          />
          <Area
            type="monotone"
            dataKey="productive"
            stroke="#36816a"
            fill="url(#areaFill)"
            strokeWidth={2}
            isAnimationActive={settings.motion}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
  if (login)
    return (
      <main className="login">
        <div className="login-card">
          <div className="brand">
            <IconActivity size={25} />
            WorkPulse
            <span className="brand-dot" />
          </div>
          <img
            src="/assets/team-illustration.png"
            alt="Двое коллег за ноутбуком"
          />
          <span className="eyebrow">ДЕМОНСТРАЦИОННАЯ СРЕДА</span>
          <h1>
            Рабочий день.
            <br />В понятном ритме.
          </h1>
          <p>
            Изучите команду, активность и события на примере демонстрационной
            организации.
          </p>
          <label>
            Ваша роль
            <select value={role} onChange={(e) => setRole(e.target.value)}>
              {roles.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </select>
          </label>
          <button className="primary full" onClick={() => setLogin(false)}>
            Открыть WorkPulse <IconArrowRight size={17} />
          </button>
          <small>Без регистрации. Все данные вымышлены.</small>
        </div>
      </main>
    );
  return (
    <MotionConfig reducedMotion={settings.motion ? "user" : "always"}>
      <div className="app">
        <header className="masthead">
          <div className="header-inner">
            <button
              className="brand"
              onClick={() => navigate("Обзор")}
              aria-label="WorkPulse — обзор"
            >
              <IconActivity size={25} />
              WorkPulse
              <span className="brand-dot" />
            </button>
            <div className="workspace">
              Студия <span className="demo-label">Demo</span>
            </div>
            <div className="header-actions">
              <button
                className="icon-button"
                aria-label="Поиск сотрудников"
                onClick={() => setSearchOpen(true)}
              >
                <IconSearch size={19} />
              </button>
              <div className="popover-anchor">
                <button
                  className="icon-button"
                  aria-label="Уведомления"
                  aria-expanded={notification}
                  onClick={() => {
                    setNotification(!notification);
                    setProfile(false);
                  }}
                >
                  <IconBell size={19} />
                  {newEvents.length > 0 && <i className="notification-dot" />}
                </button>
                {notification && (
                  <div className="popover notification-pop">
                    <strong>Требуют внимания · {newEvents.length}</strong>
                    {newEvents.length ? (
                      newEvents.map((e) => (
                        <button
                          key={e.id}
                          onClick={() => {
                            setEvent(e);
                            setNotification(false);
                          }}
                        >
                          {e.title}
                          <small>
                            {e.person} · {e.time}
                          </small>
                        </button>
                      ))
                    ) : (
                      <p>Все события рассмотрены</p>
                    )}
                  </div>
                )}
              </div>
              <div className="popover-anchor">
                <button
                  className="profile-button"
                  aria-expanded={profile}
                  onClick={() => {
                    setProfile(!profile);
                    setNotification(false);
                  }}
                >
                  <span className="avatar sage">ЕК</span>
                  <span className="profile-text">
                    {name}
                    <small>{role}</small>
                  </span>
                  <IconChevronDown size={14} />
                </button>
                {profile && (
                  <div className="popover">
                    <strong>Демороль</strong>
                    {roles.map((r) => (
                      <button
                        key={r}
                        onClick={() => {
                          setRole(r);
                          setProfile(false);
                          notify(`Роль: ${r}`);
                        }}
                      >
                        {r}
                        {r === role && <IconCheck size={15} />}
                      </button>
                    ))}
                    <button
                      onClick={() => {
                        setLogin(true);
                        setProfile(false);
                      }}
                    >
                      <IconLogout size={16} />
                      Экран входа
                    </button>
                  </div>
                )}
              </div>
            </div>
          </div>
        </header>
        <nav className="navigation" aria-label="Основная навигация">
          <div className="nav-inner">
            {tabs.map((tab) => (
              <button
                key={tab}
                onClick={() => navigate(tab)}
                className={page === tab ? "nav-tab active" : "nav-tab"}
                aria-current={page === tab ? "page" : undefined}
              >
                {tab}
                {tab === "Нарушения" && newEvents.length > 0 && (
                  <span className="nav-count">{newEvents.length}</span>
                )}
                {page === tab && (
                  <motion.span
                    layoutId="nav-indicator"
                    className="nav-indicator"
                  />
                )}
              </button>
            ))}
          </div>
        </nav>
        <main className="main">
          <div className="page-heading">
            <div>
              <h1>{page === "Обзор" ? "Обзор команды" : page}</h1>
              <p>
                {page === "Обзор"
                  ? "Понедельник, 12 октября 2026"
                  : "Студия · демонстрационная организация"}
              </p>
            </div>
            <div className="heading-actions">
              <label className="date-control">
                <IconCalendar size={16} />
                <input
                  aria-label="Дата просмотра"
                  type="date"
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                />
              </label>
              <button className="primary" onClick={() => setReport(true)}>
                Открыть отчёт <IconArrowUpRight size={16} />
              </button>
            </div>
          </div>
          {date !== "2026-10-12" && (
            <div className="demo-notice">
              Выбрана дата {date}. В прототипе показан пример рабочего дня 12
              октября.
            </div>
          )}
          <AnimatePresence mode="wait">
            <motion.div
              key={page}
              initial={{ opacity: 0, y: 7 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -4 }}
              transition={{ duration: 0.19 }}
            >
              {page === "Обзор" && (
                <>
                  <section className="welcome">
                    <div>
                      <span className="eyebrow">WORKPULSE / СЕГОДНЯ</span>
                      <h2>Всё важное о рабочем дне</h2>
                      <p>
                        Команда в своём ритме. А вы — в курсе происходящего.
                      </p>
                    </div>
                    <img
                      src="/assets/team-illustration.png"
                      alt="Минималистичная иллюстрация коллег за ноутбуком"
                    />
                  </section>
                  <section className="metrics" aria-label="Показатели команды">
                    <div className="metric">
                      <span className="metric-icon">
                        <IconUsers size={21} />
                      </span>
                      <div>
                        <small>Сейчас в работе</small>
                        <div>
                          <b>
                            {
                              people.filter((p) => p.status === "В работе")
                                .length
                            }
                          </b>
                          <span>из {people.length} сотрудников</span>
                        </div>
                      </div>
                    </div>
                    <div className="metric">
                      <span className="metric-icon">
                        <IconActivity size={21} />
                      </span>
                      <div>
                        <small>Средняя активность</small>
                        <div>
                          <b>
                            {Math.round(
                              people.reduce((s, p) => s + p.score, 0) /
                                people.length,
                            )}
                            <em>%</em>
                          </b>
                          <span className="positive">
                            ↗ 4% к прошлой неделе
                          </span>
                        </div>
                      </div>
                    </div>
                    <button
                      className="metric metric-button"
                      onClick={() => navigate("Нарушения")}
                    >
                      <span className="metric-icon warning">
                        <IconAlertCircle size={21} />
                      </span>
                      <div>
                        <small>Требуют внимания</small>
                        <div>
                          <b>{newEvents.length}</b>
                          <span>
                            события сегодня <IconArrowRight size={14} />
                          </span>
                        </div>
                      </div>
                    </button>
                  </section>
                  <div className="dashboard-grid">
                    <section className="panel activity-panel">
                      <div className="panel-heading">
                        <div>
                          <h2>Активность команды</h2>
                          <p>Сегодня · 09:00–18:00</p>
                        </div>
                        <select
                          aria-label="Отдел на дашборде"
                          value={department}
                          onChange={(e) => setDepartment(e.target.value)}
                        >
                          <option>Все отделы</option>
                          {[
                            "Дизайн",
                            "Разработка",
                            "HR",
                            "Маркетинг",
                            "Аналитика",
                          ].map((d) => (
                            <option key={d}>{d}</option>
                          ))}
                        </select>
                      </div>
                      <div className="timeline-axis">
                        <span>Сотрудник</span>
                        <div>
                          {["09:00", "12:00", "15:00", "18:00"].map((t) => (
                            <span key={t}>{t}</span>
                          ))}
                        </div>
                        <span>Время</span>
                      </div>
                      {filtered.slice(0, 6).map((person, i) => (
                        <button
                          className="activity-row"
                          key={person.id}
                          onClick={() => setSelected(person)}
                        >
                          <span className="person">
                            <Avatar person={person} />
                            <span>
                              <strong>{person.name}</strong>
                              <small>{person.job}</small>
                            </span>
                          </span>
                          <Timeline index={i} />
                          <span className="time-cell">
                            <strong>{person.time}</strong>
                            <small
                              className={
                                person.status === "В работе" ? "positive" : ""
                              }
                            >
                              {person.status}
                            </small>
                          </span>
                        </button>
                      ))}
                      <div className="panel-bottom">
                        <div className="legend">
                          <span>
                            <i className="green" />
                            Работа
                          </span>
                          <span>
                            <i className="yellow" />
                            Перерыв
                          </span>
                          <span>
                            <i className="coral" />
                            Отклонение
                          </span>
                          <span>
                            <i className="grey" />
                            Нет данных
                          </span>
                        </div>
                        <button
                          className="text-button"
                          onClick={() => navigate("Сотрудники")}
                        >
                          Вся команда <IconArrowRight size={15} />
                        </button>
                      </div>
                    </section>
                    <section className="panel events-panel">
                      <div className="panel-heading">
                        <div>
                          <h2>События дня</h2>
                          <p>То, что стоит проверить</p>
                        </div>
                        <span className="counter">{newEvents.length}</span>
                      </div>
                      {newEvents.map((e) => (
                        <button
                          key={e.id}
                          className="event-row"
                          onClick={() => setEvent(e)}
                        >
                          <span
                            className={
                              "event-icon " +
                              (e.type === "Нарушение"
                                ? "peach"
                                : e.type === "Система"
                                  ? "neutral"
                                  : "sand")
                            }
                          >
                            {e.type === "Система" ? (
                              <IconWifiOff size={17} />
                            ) : (
                              <IconAlertCircle size={17} />
                            )}
                          </span>
                          <span>
                            <small>
                              {e.time} · {e.type}
                            </small>
                            <strong>{e.title}</strong>
                            <p>{e.person}</p>
                          </span>
                          <IconArrowUpRight size={14} />
                        </button>
                      ))}
                      {!newEvents.length && (
                        <div className="empty">
                          <IconCheck size={24} />
                          <h3>Всё рассмотрено</h3>
                          <p>Новых событий нет</p>
                        </div>
                      )}
                      <button
                        className="text-button events-link"
                        onClick={() => navigate("Нарушения")}
                      >
                        Все события <IconArrowRight size={15} />
                      </button>
                      <div className="quiet-note">
                        <span className="live-dot" />
                        Агенты подключены: {people.length - 1} из{" "}
                        {people.length}
                        <small>Данные обновлены в 14:30 · Demo</small>
                      </div>
                    </section>
                  </div>
                  <footer className="page-footer">
                    <span>
                      Показатели помогают разобраться в контексте, а не
                      оценивать людей.
                    </span>
                    <span>Все данные демонстрационные</span>
                  </footer>
                </>
              )}
              {page === "Сотрудники" && (
                <section className="panel">
                  <div className="panel-heading">
                    <div>
                      <h2>
                        Вся команда{" "}
                        <span className="subtle">{people.length}</span>
                      </h2>
                      <p>Рабочий день каждого — в одном месте</p>
                    </div>
                    <div className="filters">
                      <label className="search-field">
                        <IconSearch size={17} />
                        <input
                          placeholder="Найти сотрудника"
                          aria-label="Найти сотрудника"
                          value={search}
                          onChange={(e) => setSearch(e.target.value)}
                        />
                      </label>
                      <select
                        aria-label="Фильтр отдела"
                        value={department}
                        onChange={(e) => setDepartment(e.target.value)}
                      >
                        <option>Все отделы</option>
                        {[
                          "Дизайн",
                          "Разработка",
                          "HR",
                          "Маркетинг",
                          "Аналитика",
                        ].map((d) => (
                          <option key={d}>{d}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                  <div className="table-scroll">
                    <table>
                      <thead>
                        <tr>
                          <th>Сотрудник</th>
                          <th>Отдел</th>
                          <th>Активность</th>
                          <th>Рабочее время</th>
                          <th>Статус</th>
                          <th />
                        </tr>
                      </thead>
                      <tbody>
                        {filtered.map((p) => (
                          <tr key={p.id}>
                            <td>
                              <button
                                className="person person-button"
                                onClick={() => setSelected(p)}
                              >
                                <Avatar person={p} />
                                <span>
                                  <strong>{p.name}</strong>
                                  <small>{p.job}</small>
                                </span>
                              </button>
                            </td>
                            <td>{p.department}</td>
                            <td>
                              <div className="score">
                                <span>{p.score}%</span>
                                <i style={{ width: p.score * 0.7 }} />
                              </div>
                            </td>
                            <td>{p.time}</td>
                            <td>
                              <Pill
                                kind={
                                  p.status === "В работе"
                                    ? "success"
                                    : p.status === "Нет связи"
                                      ? "muted"
                                      : "attention"
                                }
                              >
                                {p.status}
                              </Pill>
                            </td>
                            <td>
                              <button
                                className="icon-button"
                                aria-label={"Открыть " + p.name}
                                onClick={() => setSelected(p)}
                              >
                                <IconArrowUpRight size={17} />
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                  {!filtered.length && (
                    <div className="empty">
                      <h3>Сотрудники не найдены</h3>
                      <p>Попробуйте другое имя или отдел.</p>
                    </div>
                  )}
                  <div className="panel-bottom">
                    <small>
                      Показано {filtered.length} из {people.length}
                    </small>
                    <span className="demo-label">Demo data</span>
                  </div>
                </section>
              )}
              {page === "Нарушения" && (
                <section className="panel">
                  <div className="panel-heading">
                    <div>
                      <h2>Внимание к деталям</h2>
                      <p>Проверяйте причины и учитывайте контекст</p>
                    </div>
                    <select
                      value={eventFilter}
                      aria-label="Статус события"
                      onChange={(e) => setEventFilter(e.target.value)}
                    >
                      <option>Все события</option>
                      <option>Новое</option>
                      <option>Рассмотрено</option>
                    </select>
                  </div>
                  {events
                    .filter(
                      (e) =>
                        eventFilter === "Все события" ||
                        e.status === eventFilter,
                    )
                    .map((e) => (
                      <button
                        className="event-wide"
                        key={e.id}
                        onClick={() => {
                          setEvent(e);
                          setComment("");
                        }}
                      >
                        <span className="event-icon sand">
                          <IconAlertCircle size={19} />
                        </span>
                        <span>
                          <strong>{e.title}</strong>
                          <small>
                            {e.person} · {e.time} · {e.type}
                          </small>
                        </span>
                        <Pill
                          kind={e.status === "Новое" ? "attention" : "success"}
                        >
                          {e.status}
                        </Pill>
                        <IconArrowUpRight size={16} />
                      </button>
                    ))}
                  {!events.some(
                    (e) =>
                      eventFilter === "Все события" || e.status === eventFilter,
                  ) && (
                    <div className="empty">
                      <h3>Событий пока нет</h3>
                      <p>Измените фильтр, чтобы увидеть другие события.</p>
                    </div>
                  )}
                </section>
              )}
              {page === "Политики" && (
                <>
                  <div className="section-intro">
                    <p>
                      Понятные правила рабочего дня.
                      <br />
                      <span>Настройки применяются только к демоданным.</span>
                    </p>
                    <button
                      className="primary"
                      onClick={() =>
                        setPolicy({
                          id: Date.now(),
                          name: "",
                          scope: "Все отделы",
                          hours: "09:00 — 18:00",
                          limit: 20,
                          enabled: true,
                        })
                      }
                    >
                      <IconPlus size={16} />
                      Новая политика
                    </button>
                  </div>
                  <div className="policy-grid">
                    {policies.map((p) => (
                      <section className="panel policy-card" key={p.id}>
                        <div className="policy-top">
                          <span className="metric-icon">
                            <IconAdjustments size={20} />
                          </span>
                          <button
                            className={"switch " + (p.enabled ? "on" : "")}
                            role="switch"
                            aria-checked={p.enabled}
                            aria-label={"Включить " + p.name}
                            onClick={() =>
                              setPolicies(
                                policies.map((x) =>
                                  x.id === p.id
                                    ? { ...x, enabled: !x.enabled }
                                    : x,
                                ),
                              )
                            }
                          >
                            <span />
                          </button>
                        </div>
                        <h2>{p.name}</h2>
                        <Pill>{p.scope}</Pill>
                        <dl>
                          <div>
                            <dt>Рабочие часы</dt>
                            <dd>{p.hours}</dd>
                          </div>
                          <div>
                            <dt>Лимит перерыва</dt>
                            <dd>{p.limit} минут</dd>
                          </div>
                        </dl>
                        <button
                          className="secondary full"
                          onClick={() => setPolicy({ ...p })}
                        >
                          Изменить настройки <IconArrowUpRight size={16} />
                        </button>
                      </section>
                    ))}
                  </div>
                </>
              )}
              {page === "Отчёты" && (
                <>
                  <div className="report-top">
                    <section className="panel report-summary">
                      <span className="eyebrow">РИТМ КОМАНДЫ</span>
                      <h2>За цифрами — рабочие дни.</h2>
                      <p>
                        Динамика помогает заметить изменения и задать правильные
                        вопросы.
                      </p>
                      <div className="segmented">
                        {["Неделя", "Месяц"].map((p) => (
                          <button
                            key={p}
                            className={period === p ? "selected" : ""}
                            onClick={() => setPeriod(p)}
                          >
                            {p}
                          </button>
                        ))}
                      </div>
                      <Chart />
                      <small>
                        Средняя активность по будним дням · пример{" "}
                        {period === "Неделя" ? "недели" : "месяца"}
                      </small>
                    </section>
                    <section className="panel report-side">
                      <IconChartBar size={24} />
                      <h2>Обзор команды</h2>
                      <p>
                        Активность, рабочее время и события для {people.length}{" "}
                        сотрудников.
                      </p>
                      <strong className="big-number">
                        {period === "Неделя" ? "86" : "82"}
                        <small>%</small>
                      </strong>
                      <span className="positive">Средняя активность</span>
                      <button
                        className="primary full"
                        onClick={() => setReport(true)}
                      >
                        Посмотреть отчёт <IconArrowRight size={16} />
                      </button>
                    </section>
                  </div>
                  <section className="panel report-download">
                    <div>
                      <IconFileText size={20} />
                      <span>
                        <strong>Данные для собственного анализа</strong>
                        <small>
                          CSV · сотрудники, отделы, активность и рабочее время
                        </small>
                      </span>
                    </div>
                    <button className="secondary" onClick={exportCSV}>
                      <IconDownload size={16} />
                      Скачать CSV
                    </button>
                  </section>
                </>
              )}
              {page === "Настройки" && (
                <div className="settings-grid">
                  <section className="panel settings-panel">
                    <h2>Ваш профиль</h2>
                    <p>Настройки демонстрационной сессии</p>
                    <form
                      onSubmit={(e) => {
                        e.preventDefault();
                        notify("Профиль обновлён");
                      }}
                    >
                      <label>
                        Имя
                        <input
                          required
                          value={name}
                          onChange={(e) => setName(e.target.value)}
                        />
                      </label>
                      <label>
                        Роль
                        <select
                          value={role}
                          onChange={(e) => setRole(e.target.value)}
                        >
                          {roles.map((r) => (
                            <option key={r}>{r}</option>
                          ))}
                        </select>
                      </label>
                      <button className="primary">Сохранить изменения</button>
                    </form>
                  </section>
                  <section className="panel settings-panel">
                    <h2>Комфорт в деталях</h2>
                    <div className="setting-row">
                      <div>
                        <strong>Уведомления</strong>
                        <small>Показывать сообщения о действиях</small>
                      </div>
                      <button
                        className={"switch " + (settings.notify ? "on" : "")}
                        role="switch"
                        aria-checked={settings.notify}
                        aria-label="Уведомления о действиях"
                        onClick={() =>
                          setSettings({ ...settings, notify: !settings.notify })
                        }
                      >
                        <span />
                      </button>
                    </div>
                    <div className="setting-row">
                      <div>
                        <strong>Плавные переходы</strong>
                        <small>
                          Системная настройка уменьшения движения также
                          учитывается
                        </small>
                      </div>
                      <button
                        className={"switch " + (settings.motion ? "on" : "")}
                        role="switch"
                        aria-checked={settings.motion}
                        aria-label="Плавные переходы"
                        onClick={() =>
                          setSettings({ ...settings, motion: !settings.motion })
                        }
                      >
                        <span />
                      </button>
                    </div>
                    <div className="quiet-note">
                      <strong>Локальный прототип</strong>
                      <small>
                        Светлая тема · вымышленные данные · без серверных
                        запросов. Демороли показывают сценарии, а не реальную
                        авторизацию.
                      </small>
                    </div>
                  </section>
                </div>
              )}
            </motion.div>
          </AnimatePresence>
        </main>
        <Modal
          open={!!selected}
          onClose={() => setSelected(null)}
          title="Карточка сотрудника"
          drawer
        >
          {selected && (
            <>
              <div className="employee-intro">
                <Avatar person={selected} />
                <h2>{selected.name}</h2>
                <p>
                  {selected.job} · {selected.department}
                </p>
                <Pill kind="success">{selected.status}</Pill>
              </div>
              <div className="detail-metrics">
                <div>
                  <small>Рабочее время</small>
                  <strong>{selected.time}</strong>
                </div>
                <div>
                  <small>Активность</small>
                  <strong>{selected.score}%</strong>
                </div>
              </div>
              <h3>Сегодня, 12 октября</h3>
              <Timeline index={selected.id - 1} />
              <div className="detail-axis">
                <span>09:00</span>
                <span>14:30</span>
                <span>18:00</span>
              </div>
              <h3>Хронология дня</h3>
              <div className="chronology">
                {[
                  ["09:00", "Начало рабочего дня", "Агент подключён"],
                  ["11:20", "Перерыв", "15 минут"],
                  ["11:35", "Возвращение к работе", "Рабочая активность"],
                  ["14:30", "Последнее обновление", "Демонстрационные данные"],
                ].map(([time, title, sub]) => (
                  <div key={time}>
                    <time>{time}</time>
                    <span>
                      <strong>{title}</strong>
                      <small>{sub}</small>
                    </span>
                  </div>
                ))}
              </div>
              <div className="quiet-note">
                Сведения отображаются в рамках согласованной политики
                мониторинга.
              </div>
            </>
          )}
        </Modal>
        <Modal
          open={!!event}
          onClose={() => setEvent(null)}
          title="Разбор события"
        >
          {event && (
            <>
              <Pill kind="attention">{event.type}</Pill>
              <h2>{event.title}</h2>
              <p className="subtle">
                {event.person} · 12 октября, {event.time}
              </p>
              <p className="event-detail">{event.detail}</p>
              {comments[event.id] && (
                <div className="quiet-note">
                  <strong>Сохранённый комментарий</strong>
                  <p>{comments[event.id]}</p>
                </div>
              )}
              <label>
                Комментарий
                <textarea
                  placeholder="Добавьте контекст или причину…"
                  value={comment}
                  onChange={(e) => setComment(e.target.value)}
                />
              </label>
              <div className="dialog-actions">
                <button className="secondary" onClick={() => setEvent(null)}>
                  Закрыть
                </button>
                <button
                  className="primary"
                  disabled={event.status === "Рассмотрено"}
                  onClick={resolveEvent}
                >
                  <IconCheck size={16} />
                  {event.status === "Рассмотрено"
                    ? "Рассмотрено"
                    : "Отметить рассмотренным"}
                </button>
              </div>
            </>
          )}
        </Modal>
        <Modal
          open={!!policy}
          onClose={() => setPolicy(null)}
          title="Политика мониторинга"
        >
          {policy && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                setPolicies(
                  policies.some((p) => p.id === policy.id)
                    ? policies.map((p) => (p.id === policy.id ? policy : p))
                    : [...policies, policy],
                );
                setPolicy(null);
                notify("Политика сохранена в демосессии");
              }}
            >
              <label>
                Название
                <input
                  required
                  value={policy.name}
                  onChange={(e) =>
                    setPolicy({ ...policy, name: e.target.value })
                  }
                />
              </label>
              <label>
                Отдел
                <select
                  value={policy.scope}
                  onChange={(e) =>
                    setPolicy({ ...policy, scope: e.target.value })
                  }
                >
                  {[
                    "Все отделы",
                    "Разработка",
                    "Дизайн",
                    "HR",
                    "Маркетинг",
                    "Аналитика",
                  ].map((d) => (
                    <option key={d}>{d}</option>
                  ))}
                </select>
              </label>
              <label>
                Рабочие часы
                <input
                  required
                  value={policy.hours}
                  onChange={(e) =>
                    setPolicy({ ...policy, hours: e.target.value })
                  }
                />
              </label>
              <label>
                Лимит перерыва, минут
                <input
                  type="number"
                  min="5"
                  max="120"
                  required
                  value={policy.limit}
                  onChange={(e) =>
                    setPolicy({ ...policy, limit: Number(e.target.value) })
                  }
                />
              </label>
              <div className="dialog-actions">
                <button
                  type="button"
                  className="secondary"
                  onClick={() => setPolicy(null)}
                >
                  Отмена
                </button>
                <button className="primary">Сохранить</button>
              </div>
            </form>
          )}
        </Modal>
        <Modal
          open={report}
          onClose={() => setReport(false)}
          title="Отчёт об активности"
        >
          <span className="eyebrow">ДЕМОНСТРАЦИОННЫЙ ОТЧЁТ</span>
          <h2>Команда в цифрах</h2>
          <p className="subtle">
            12 октября 2026 · {people.length} сотрудников
          </p>
          <Chart />
          <div className="dialog-actions">
            <button className="secondary" onClick={() => setReport(false)}>
              Закрыть
            </button>
            <button className="primary" onClick={exportCSV}>
              <IconDownload size={16} />
              Скачать CSV
            </button>
          </div>
        </Modal>
        <Modal
          open={searchOpen}
          onClose={() => {
            setSearchOpen(false);
            setSearch("");
          }}
          title="Найти в WorkPulse"
        >
          <label className="search-field">
            <IconSearch size={18} />
            <input
              autoFocus
              aria-label="Глобальный поиск сотрудников"
              placeholder="Имя сотрудника или должность"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <div className="search-results">
            {people
              .filter((p) =>
                (p.name + " " + p.job)
                  .toLowerCase()
                  .includes(search.toLowerCase()),
              )
              .map((p) => (
                <button
                  key={p.id}
                  className="person"
                  onClick={() => {
                    setSearchOpen(false);
                    setSearch("");
                    setSelected(p);
                  }}
                >
                  <Avatar person={p} />
                  <span>
                    <strong>{p.name}</strong>
                    <small>{p.job}</small>
                  </span>
                  <IconArrowUpRight size={15} />
                </button>
              ))}
          </div>
        </Modal>
        <AnimatePresence>
          {toast && settings.notify && (
            <motion.div
              role="status"
              className="toast"
              initial={{ opacity: 0, y: 16 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: 12 }}
            >
              <IconCheck size={17} />
              {toast}
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </MotionConfig>
  );
}
