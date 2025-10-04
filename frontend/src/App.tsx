import { BrowserRouter, Routes, Route, Link, useLocation } from 'react-router-dom'
import Dashboard from './pages/Dashboard'
import Configuration from './pages/Configuration'

function Layout({ children }: { children: React.ReactNode }) {
  const location = useLocation()

  const isActive = (path: string) => location.pathname === path

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200">
        <div className="px-6 py-4">
          <h1 className="text-xl font-bold text-gray-900">Industrial ADAM Logger</h1>
        </div>
        <nav className="px-6 flex gap-6 border-t border-gray-100">
          <Link
            to="/"
            className={`py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              isActive('/')
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-600 hover:text-gray-900 hover:border-gray-300'
            }`}
          >
            Dashboard
          </Link>
          <Link
            to="/configuration"
            className={`py-3 px-1 border-b-2 font-medium text-sm transition-colors ${
              isActive('/configuration')
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-600 hover:text-gray-900 hover:border-gray-300'
            }`}
          >
            Configuration
          </Link>
        </nav>
      </header>
      <main className="p-6">
        {children}
      </main>
    </div>
  )
}

function App() {
  return (
    <BrowserRouter>
      <Layout>
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/configuration" element={<Configuration />} />
        </Routes>
      </Layout>
    </BrowserRouter>
  )
}

export default App
