import { Component, type ReactNode } from "react";
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  render() {
    if (this.state.failed) return <main className="login"><section className="login-card"><h1>Не удалось отобразить страницу</h1><p role="alert">Обновите приложение. Сохранённые на сервере данные останутся доступны.</p><button className="primary" onClick={() => window.location.reload()}>Обновить</button></section></main>;
    return this.props.children;
  }
}
